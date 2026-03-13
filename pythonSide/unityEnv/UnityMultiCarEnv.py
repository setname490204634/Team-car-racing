import gymnasium as gym
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

class UnityMultiCarEnv(gym.Env):

    def __init__(
        self,
        number_of_agents: int,
        unity_exe_path: str = r"TeamRacing\builds\TeamRacing.exe",
        log_dir: str = r"pythonSide\env_logs",
        debug: bool = False,
        run_headless: bool = False
    ):

        super().__init__()

        self.agent_ids = [f"agent_{i}" for i in range(number_of_agents)]
        self.agentCount = number_of_agents

        self.agents = {
            aid: agent.agent(
                id=aid,
                unity_car_id=i,
                debug=debug
            )
            for i, aid in enumerate(self.agent_ids)
        }

        self.observation_space = spaces.Dict({
            aid: spaces.Box(
                low=0.0,
                high=1.0,
                shape=(12, 64, 128),
                dtype=np.float32
            )
            for aid in self.agent_ids
        })

        self.action_space = spaces.Dict({
            aid: spaces.Box(
                low=-1.0,
                high=1.0,
                shape=(2,),
                dtype=np.float32
            )
            for aid in self.agent_ids
        })

        # networking
        self._control_sock, self.control_port = get_os_assigned_port()
        self._car_sock, self.car_instr_port = get_os_assigned_port()
        self._obs_sock, self.obs_port = get_os_assigned_port()

        self.unity_exe_path = unity_exe_path
        self.unity_process = None
        self.run_headless = run_headless

        self.max_steps = 1024
        self.episodeCount = 0

        env_folder = get_next_env_folder(log_dir)

        self.tb_writer = SummaryWriter(
            log_dir=os.path.join(log_dir, env_folder)
        )

        self.debug = debug

        self._launch_unity()

        wait_for_port("127.0.0.1", self.control_port)
        wait_for_port("127.0.0.1", self.car_instr_port)

        self.obs_receiver = ObservationReceiver(
            host="0.0.0.0",
            port=self.obs_port
        )
        self.obs_receiver.start()

        self.sendCommandToUnity(CommandCode.ChangeMap, 4)

        if run_headless:
            self.sendCommandToUnity(CommandCode.UnlimitedSpeed)

        self.sendCommandToUnity(CommandCode.SetMaxSteeringChange, 6)
        
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
            "--carCount", str(self.agentCount)
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
        self.mapSwitchCount += 1
        self.sendCommandToUnity(CommandCode.StopSimulation)

        if self.mapSwitchCount % self.changeMapEvery == 0:
            self.sendCommandToUnity(CommandCode.ChangeMap, random.randint(0, self.maxMapIndex))
        time.sleep(0.03) #magic wait to let the map load before cars
        
        self.sendCommandToUnity(CommandCode.ShuffleCars)
        self.sendCommandToUnity(CommandCode.Reset)
        self.sendCommandToUnity(CommandCode.StartSimulation)

        while not self.obs_receiver.has_min_observations(self.agentCount):
            time.sleep(0.0001)

        self.episode_rewards_per_category = Rewards()  # reset at the start of the episode
        
        packets = self.obs_receiver.collect_observations()
        
        observations = {}
        
        for packet in packets:
            carID = packet.car_id
            aID = self.agent_ids[carID]
            
            agent = self.agents[aID]
            
            rgb = packet.image
            rgb = cv2.flip(rgb, 0)
            agent.initFrameStack(rgb)
            
            observations[aID] = agent.get_observation()
            
        return observations, {}

    def step(self, action):
        for aid, action in actions.items():

            ag = self.agents[aid]

            steer, throttle = ag.encode_action(action)

            sender.send_car_instruction(
                ag.unity_car_id,
                steer,
                throttle,
                self.car_instr_port
            )

        while not self.obs_receiver.has_min_observations(len(self.agent_ids)):
            time.sleep(0.0001)

        packets = self.obs_receiver.collect_observations()

        obs = {}
        rewards = {}
        terminated = {}
        truncated = {}
        info = {}
        
        for packet in packets:

            car_id = packet.car_id
            aid = self.agent_ids[car_id]

            ag = self.agents[aid]

            o, r, term, trunc = ag.update_from_packet(packet)

            obs[aid] = o
            rewards[aid] = r
            terminated[aid] = term
            truncated[aid] = trunc

        if any(terminated.values()) or any(truncated.values()):
            ...
            #some logging

        return obs, rewards, terminated, truncated, info
    

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
        
        if hasattr(self, "tb_writer"):
            self.tb_writer.flush()
            self.tb_writer.close()
            
    def __del__(self):
        self.close()
