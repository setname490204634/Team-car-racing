import socket
import struct
import numpy as np
import threading
from collections import deque

class Observation:
    """Represents a single car observation packet."""
    def __init__(self, car_id: int, speed: int, steer: int, reward: float, image: np.ndarray):
        self.car_id = car_id
        self.speed = speed
        self.steer = steer
        self.reward = reward
        self.image = image

class ObservationReceiver:
    """TCP server to receive Unity observation packets and store them in a buffer."""
    
    def __init__(self, host="0.0.0.0", port=5007, buffer_size=32):
        self.host = host
        self.port = port
        self.buffer_size = buffer_size
        self.observations = deque(maxlen=buffer_size)  # Holds the most recent observations
        self.lock = threading.Lock()
        self.running = False

        self.CAM_WIDTH = 64
        self.CAM_HEIGHT = 64
        self.MERGED_WIDTH = self.CAM_WIDTH * 2
        self.MERGED_HEIGHT = self.CAM_HEIGHT
        self.BYTES_PER_PIXEL = 3
        self.HEADER_SIZE = 10
        self.expected_packet_size = self.HEADER_SIZE + self.MERGED_WIDTH * self.MERGED_HEIGHT * self.BYTES_PER_PIXEL

    def start(self):
        """Start the TCP server in a separate thread."""
        self.running = True
        threading.Thread(target=self._receive_loop, daemon=True).start()
        print(f"ObservationReceiver started on {self.host}:{self.port}")

    def _receive_loop(self):
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as server:
            server.bind((self.host, self.port))
            server.listen()
            print("Waiting for Unity to connect...")

            while self.running:
                conn, addr = server.accept()
                print(f"Unity connected from {addr}")
                with conn:
                    buffer = b""
                    while self.running:
                        try:
                            data = conn.recv(4096)
                            if not data:
                                print("Unity disconnected.")
                                break
                            buffer += data

                            while len(buffer) >= self.expected_packet_size:
                                packet = buffer[:self.expected_packet_size]

                                # Parse header
                                header = packet[:self.HEADER_SIZE]
                                speed, steer, car_id, reward = struct.unpack('<BBif', header)

                                # Parse image
                                img_bytes = packet[self.HEADER_SIZE:]
                                img = np.frombuffer(img_bytes, dtype=np.uint8).reshape(
                                    (self.MERGED_HEIGHT, self.MERGED_WIDTH, 3)
                                )

                                obs = Observation(car_id, speed, steer, reward, img)
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
        """Return all current observations and empty the buffer."""
        with self.lock:
            obs_list = list(self.observations)
            self.observations.clear()
        return obs_list

    def has_min_observations(self, n):
        """Return True if there are at least n observations in the buffer."""
        with self.lock:
            return len(self.observations) >= n

    def stop(self):
        self.running = False
