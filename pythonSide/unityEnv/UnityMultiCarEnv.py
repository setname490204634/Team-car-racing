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
from .envUtils import wait_for_port, get_os_assigned_port, get_next_env_folder
from .agent import *

class UnityMultiCarEnv(MultiAgentEnv):

    def __init__(
        self,
        number_of_agents: int,
        unity_exe_path: str = r"TeamRacing\builds\TeamRacing.exe",
        base_log_dir: str = r"pythonSide\env_logs",
        debug: bool = False,
        run_headless: bool = False,
        maxSteps: int = 512,
        grayScaleHisotry: bool = True
    ):

        super().__init__()
        self.agent_ids = [f"agent_{i}" for i in range(number_of_agents)]
        self.possible_agents = self.agent_ids
        self.agentCount = number_of_agents
        
        log_dir = get_next_env_folder(base_log_dir)

        self.agents = {
            aid: agent(
                id=aid,
                unity_car_id=i,
                maxSteps=maxSteps,
                logdir=log_dir,
                debug=debug,
                fatalCollision=True,
                grayScaleHisotry=grayScaleHisotry
            )
            for i, aid in enumerate(self.agent_ids)
        }

        #grayscale obs switch
        if grayScaleHisotry:
            obs_shape = (64, 128, 6)  # HWC format: Height, Width, Channels
        else:
            obs_shape = (64, 128, 12)  # HWC format: Height, Width, Channels

        self.observation_spaces = {
            aid: spaces.Box(
                low=0.0,
                high=1.0,
                shape=obs_shape,
                dtype=np.float32
            )
            for aid in self.agent_ids
        }

        self.action_spaces = {
            aid: spaces.Box(
                low=-1.0,
                high=1.0,
                shape=(2,),
                dtype=np.float32
            )
            for aid in self.agent_ids
        }

        # networking
        # self.control_port = 5005
        # self.car_instr_port = 5006
        # self.obs_port = 5007
        
        self._control_sock, self.control_port = get_os_assigned_port()
        self._car_sock, self.car_instr_port = get_os_assigned_port()
        self._obs_sock, self.obs_port = get_os_assigned_port()

        # unity
        self.unity_exe_path = unity_exe_path
        self.unity_process = None
        self.run_headless = run_headless
        self.debug = debug
        
        # between episode env state
        self.maxMapIndex = 10
        self.changeMapEvery = 500000
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

        self.sendCommandToUnity(CommandCode.ChangeMap, 6)
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
        self.stepCount += 1
        self.mapSwitchCount += 1
        self.sendCommandToUnity(CommandCode.StopSimulation)

        if self.mapSwitchCount % self.changeMapEvery == 0:
            self.sendCommandToUnity(CommandCode.ChangeMap, random.randint(0, self.maxMapIndex))
        
        self.sendCommandToUnity(CommandCode.ResetCarToRandomStartLocation)
        self.sendCommandToUnity(CommandCode.ChangeCarColoursRandomly)
        # self.sendCommandToUnity(CommandCode.ShuffleCars)
        # self.sendCommandToUnity(CommandCode.Reset)
        self.sendCommandToUnity(CommandCode.StartSimulation)

        while not self.obs_receiver.has_min_observations(self.agentCount):
            time.sleep(0.0001)
        
        packets = self.obs_receiver.collect_observations()
        
        observations = {}
        
        for packet in packets:
            carID = packet.car_id
            aID = self.agent_ids[carID]
            
            agent = self.agents[aID]
            
            rgb = packet.image
            agent.initFrameStack(rgb)
            
            observations[aID] = agent.get_observation()
            
        return observations, {}

    def step(self, action):
        for aid, action in action.items():

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
        
        obs, rewards, terminated, truncated = self.processPackets(packets)

        if any(terminated.values()):
            self.logAllAgents()
            terminated["__all__"] = True
        else:
            terminated["__all__"] = False
            
        if any(truncated.values()):
            self.logAllAgents()
            truncated["__all__"] = True
        else:
            truncated["__all__"] = False
            
        info = {}

        return obs, rewards, terminated, truncated, info
    
    def logAllAgents(self):
        for agent in self.agents.values():
            agent.logEpisode(self.stepCount)
            
    def processPackets(self, packets):
        obs = {}
        rewards = {}
        terminated = {}
        truncated = {}
        
        for packet in packets:

            car_id = packet.car_id
            aid = self.agent_ids[car_id]

            ag = self.agents[aid]

            o, r, term, trunc = ag.update_from_packet(packet)

            obs[aid] = o
            rewards[aid] = r
            terminated[aid] = term
            truncated[aid] = trunc
            
        # #observation sanity check
        # if "agent_0" in obs:
        #     o = obs["agent_0"]  # (H, W, 6) in HWC format

        #     # Convert back to uint8 for display only
        #     o = (o * 255.0).clip(0, 255).astype(np.uint8)

        #     # RGB image - channels 0-2 (already HWC, no transpose needed)
        #     rgb = o[..., :3]
        #     rgb = cv2.cvtColor(rgb, cv2.COLOR_RGB2BGR)

        #     # Individual grayscale channels
        #     gray1 = cv2.cvtColor(o[..., 3], cv2.COLOR_GRAY2BGR)
        #     gray2 = cv2.cvtColor(o[..., 4], cv2.COLOR_GRAY2BGR)
        #     gray3 = cv2.cvtColor(o[..., 5], cv2.COLOR_GRAY2BGR)

        #     # Make larger for viewing
        #     scale = 3

        #     rgb   = cv2.resize(rgb,   None, fx=scale, fy=scale, interpolation=cv2.INTER_NEAREST)
        #     gray1 = cv2.resize(gray1, None, fx=scale, fy=scale, interpolation=cv2.INTER_NEAREST)
        #     gray2 = cv2.resize(gray2, None, fx=scale, fy=scale, interpolation=cv2.INTER_NEAREST)
        #     gray3 = cv2.resize(gray3, None, fx=scale, fy=scale, interpolation=cv2.INTER_NEAREST)

        #     combined = np.hstack([rgb, gray1, gray2, gray3])

        #     cv2.imshow("Observation", combined)
        #     cv2.waitKey(1)
            
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
        
        for agent_obj in self.agents.values():
            agent_obj.close()
            
    def __del__(self):
        self.close()
