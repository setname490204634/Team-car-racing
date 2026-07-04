import gymnasium as gym
from ray.rllib.env.multi_agent_env import MultiAgentEnv
from gymnasium import spaces
import numpy as np
import time
from . import sender
from .reciever import ObservationReceiver
import subprocess
import os
from .rewards import *
import cv2
import random
from .CommandConstants import CommandCode
from torch.utils.tensorboard import SummaryWriter
from .envUtils import wait_for_port, get_os_assigned_port, get_next_env_folder
from .agent import *

class UnityCarEnv(MultiAgentEnv):

    def __init__(
        self,
        unity_exe_path: str = r"TeamRacing\builds\TeamRacing.exe",
        base_log_dir: str = r"pythonSide\env_logs",
        debug: bool = False,
        run_headless: bool = False,
        maxSteps: int = 1024
    ):
        super().__init__()

        log_dir = get_next_env_folder(base_log_dir)
        self.agent = agent(id="agent_0", unity_car_id=0, maxSteps=maxSteps, logdir=log_dir, debug=debug, fatalCollision=True)
        
        self.observation_space = spaces.Box(
            low=0.0, high=1.0,
            shape=(12, 64, 128),
            dtype=np.float32
        )
        self.action_space = spaces.Box(low=-1.0, high=1.0, shape=(2,), dtype=np.float32)

        # networking
        self._control_sock, self.control_port = get_os_assigned_port()
        self._car_sock, self.car_instr_port = get_os_assigned_port()
        self._obs_sock, self.obs_port = get_os_assigned_port()

        self.unity_exe_path = unity_exe_path
        self.unity_process = None
        self.run_headless = run_headless
        self.debug = debug
        
        self.maxMapIndex = 6
        self.changeMapEvery = 1000000
        self.mapSwitchCount = 0

        self.stepCount = 0


        self._launch_unity()

        wait_for_port("127.0.0.1", self.control_port)
        wait_for_port("127.0.0.1", self.car_instr_port)
        
        sender.init_control_socket(self.control_port)
        sender.init_car_socket(self.car_instr_port)

        self.obs_receiver = ObservationReceiver(
            host="0.0.0.0",
            port=self.obs_port
        )
        self.obs_receiver.start()

        self.sendCommandToUnity(CommandCode.ChangeMap, 4)
        self.sendCommandToUnity(CommandCode.SetLapCount, 1)
        self.sendCommandToUnity(CommandCode.SetMaxSteeringChange, 16)

        if run_headless:
            self.sendCommandToUnity(CommandCode.UnlimitedSpeed)

        
    def sendCommandToUnity(self, command, value =0 ):
        sender.send_command(command, value, self.control_port)

    def _launch_unity(self):
        #return #uncomment for manual unity launch, then default ports are expected
        if not os.path.exists(self.unity_exe_path):
            raise FileNotFoundError(f"Unity executable not found: {self.unity_exe_path}")
            
        args = [
            self.unity_exe_path,
            "--controlPort", str(self.control_port),
            "--carInstructionsPort", str(self.car_instr_port),
            "--observationPort", str(self.obs_port),
            "--carCount", str(1)
        ]
        if (self.run_headless):
            args.append("-batchmode")
        print(f"Launching Unity: {' '.join(args)}")
        self.unity_process = subprocess.Popen(
            args,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0
        )


    def reset(self, **kwargs):
        self.stepCount += 1
        self.mapSwitchCount += 1
        self.sendCommandToUnity(CommandCode.StopSimulation)

        if self.mapSwitchCount % self.changeMapEvery == 0:
            self.sendCommandToUnity(CommandCode.ChangeMap, random.randint(0, self.maxMapIndex))
        
        self.sendCommandToUnity(CommandCode.Reset)
        self.sendCommandToUnity(CommandCode.ChangeCarColoursRandomly)
        self.sendCommandToUnity(CommandCode.StartSimulation)

        while not self.obs_receiver.has_min_observations(1):
            time.sleep(0.0001)
        
        packet = self.obs_receiver.collect_observations()[-1]
        
        rgb = packet.image
        rgb = cv2.flip(rgb, 0)
        self.agent.initFrameStack(rgb)
     
        return self.agent.get_observation(), {}

    def step(self, action):
        steer, throttle = self.agent.encode_action(action)

        sender.send_car_instruction(
            self.agent.unity_car_id,
            steer,
            throttle,
            self.car_instr_port
        )

        while not self.obs_receiver.has_min_observations(1):
            time.sleep(0.0001)

        packet = self.obs_receiver.collect_observations()[-1]
        
        obs, rewards, terminated, truncated = self.processPacket(packet)
        
        if terminated:
            self.agent.logEpisode(self.stepCount)

            
        if truncated:
            self.agent.logEpisode(self.stepCount)
            
        info = {}

        return obs, rewards, terminated, truncated, info
            
    def processPacket(self, packet):
        obs, rewards, terminated, truncated = self.agent.update_from_packet(packet)
        return obs, rewards, terminated, truncated

    def close(self):
        if self.unity_process:
            print("Closing Unity process...")
            self.unity_process.terminate()
            try:
                self.unity_process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                self.unity_process.kill()
            self.unity_process = None

        obs_receiver = getattr(self, "obs_receiver", None)
        if obs_receiver:
            obs_receiver.stop()
            self.obs_receiver = None
        
        self.agent.close()

            
    def __del__(self):
        self.close()
