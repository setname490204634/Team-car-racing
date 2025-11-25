import socket
import struct
import numpy as np
import threading
from collections import deque
from rewards import Rewards

class Observation:
    """Represents a single car observation packet."""
    def __init__(self, car_id: int, speed: int, steer: int, rewards: Rewards, image: np.ndarray):
        self.car_id = car_id
        self.speed = speed / 255.0
        self.steer = (steer / 255.0) * 2.0 - 1.0
        self.rewards = rewards
        self.image = image


class ObservationReceiver:
    """TCP server to receive Unity observation packets and store them in a buffer."""
    
    def __init__(self, host="0.0.0.0", port=5007, buffer_size=32):
        self.host = host
        self.port = port
        self.buffer_size = buffer_size
        self.observations = deque(maxlen=buffer_size)
        self.lock = threading.Lock()
        self.running = False

        # camera config
        self.CAM_WIDTH = 64
        self.CAM_HEIGHT = 64
        self.MERGED_WIDTH = self.CAM_WIDTH * 2
        self.MERGED_HEIGHT = self.CAM_HEIGHT
        self.BYTES_PER_PIXEL = 3

        # Header = 1 (speed) + 1 (steer) + 4 (carID) + 200 (50 floats * 4 bytes)
        self.NUM_REWARDS = 50
        self.HEADER_SIZE = 1 + 1 + 4 + self.NUM_REWARDS * 4
        self.expected_packet_size = self.HEADER_SIZE + self.MERGED_WIDTH * self.MERGED_HEIGHT * self.BYTES_PER_PIXEL

    def start(self):
        self.running = True
        threading.Thread(target=self._receive_loop, daemon=True).start()
        print(f"ObservationReceiver started on {self.host}:{self.port}")

    def _receive_loop(self):
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as server:
            server.bind((self.host, self.port))
            server.listen()
            print("Waiting for Unity to connect to observation reciever...")

            while self.running:
                conn, addr = server.accept()
                print(f"Unity connected from {addr}")
                with conn:
                    buffer = b""
                    while self.running:
                        try:
                            data = conn.recv(32 * 1024)
                            if not data:
                                print("Unity disconnected.")
                                break
                            buffer += data
                            while len(buffer) >= self.expected_packet_size:
                                packet = buffer[:self.expected_packet_size]

                                # Parse header
                                header = packet[:self.HEADER_SIZE]
                                # Unpack speed, steer, car_id, and 10 floats
                                header_format = '<BBi' + 'f' * self.NUM_REWARDS
                                unpacked = struct.unpack(header_format, header)
                                speed, steer, car_id = unpacked[:3]
                                #!!!
                                # common bug place the number has to be change based on how many rewards are acutally used
                                #!!!
                                reward_values = unpacked[3:36]

                                rewards = Rewards(*reward_values)

                                # Parse image
                                img_bytes = packet[self.HEADER_SIZE:]
                                img = np.frombuffer(img_bytes, dtype=np.uint8).reshape(
                                    (self.MERGED_HEIGHT, self.MERGED_WIDTH, 3)
                                )

                                obs = Observation(car_id, speed, steer, rewards, img)
                                with self.lock:
                                    self.observations.append(obs)

                                buffer = buffer[self.expected_packet_size:]
                        except ConnectionResetError:
                            print("Connection reset by Unity.")
                            break
                        except Exception as e:
                            print(f"Error receiving data: {e}")
                            break

    def collect_observations(self):
        with self.lock:
            obs_list = list(self.observations)
            self.observations.clear()
        return obs_list

    def has_min_observations(self, n):
        with self.lock:
            return len(self.observations) >= n

    def stop(self):
        self.running = False
