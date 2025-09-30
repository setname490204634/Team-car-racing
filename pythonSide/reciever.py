import socket
import struct
import numpy as np

HOST = "127.0.0.1"
PORT = 5007

# Example: left/right camera resolution
CAM_WIDTH = 64
CAM_HEIGHT = 64
NUM_PIXELS = CAM_WIDTH * CAM_HEIGHT

def receive_observations():
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.connect((HOST, PORT))
        print(f"✅ Connected to Unity observation transmitter on {HOST}:{PORT}")

        buffer = b""
        while True:
            data = s.recv(4096)
            if not data:
                break
            buffer += data

            # Process packets in buffer
            while len(buffer) >= 10 + NUM_PIXELS * 2 * 2:
                # Header
                header = buffer[:10]
                speed, steer, car_id, reward = struct.unpack('<BBii', header)

                # Camera data
                cam_data_len = NUM_PIXELS * 2  # RGB565 = 2 bytes per pixel
                left_cam_start = 10
                left_cam_end = left_cam_start + cam_data_len
                right_cam_start = left_cam_end
                right_cam_end = right_cam_start + cam_data_len

                left_cam_bytes = buffer[left_cam_start:left_cam_end]
                right_cam_bytes = buffer[right_cam_start:right_cam_end]

                # Convert RGB565 to numpy array (optional)
                left_cam = np.frombuffer(left_cam_bytes, dtype=np.uint16).reshape((CAM_HEIGHT, CAM_WIDTH))
                right_cam = np.frombuffer(right_cam_bytes, dtype=np.uint16).reshape((CAM_HEIGHT, CAM_WIDTH))

                print(f"Car {car_id} | Speed {speed} | Steer {steer} | Reward {reward}")
                # print(left_cam)  # uncomment to see raw camera data

                # Remove processed packet from buffer
                buffer = buffer[10 + cam_data_len * 2:]
