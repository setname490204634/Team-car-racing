import socket
import struct
import numpy as np

# Configuration
HOST = "0.0.0.0"         # Listen on all interfaces
PORT = 5007              # Must match Unity transmitter port

CAM_WIDTH = 64            # Single camera width
CAM_HEIGHT = 64           # Single camera height

MERGED_WIDTH = CAM_WIDTH * 2
MERGED_HEIGHT = CAM_HEIGHT
BYTES_PER_PIXEL = 3       # RGB24
HEADER_SIZE = 10          # 1 speed + 1 steering + 4 carID + 4 reward

def receive_observations():
    """
    Runs a TCP server that listens for Unity observation packets.
    Each packet contains a 10-byte header followed by a merged RGB image.
    """
    expected_packet_size = HEADER_SIZE + MERGED_WIDTH * MERGED_HEIGHT * BYTES_PER_PIXEL

    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as server:
        server.bind((HOST, PORT))
        server.listen()
        print(f"Receiver listening on {HOST}:{PORT}...")

        while True:
            print("Waiting for Unity to connect...")
            conn, addr = server.accept()
            print(f"Unity connected from {addr}")

            with conn:
                buffer = b""
                while True:
                    try:
                        data = conn.recv(4096)
                        if not data:
                            print("Unity disconnected.")
                            break
                        buffer += data

                        # Process all complete packets in buffer
                        while len(buffer) >= expected_packet_size:
                            packet = buffer[:expected_packet_size]

                            # Parse header
                            header = packet[:HEADER_SIZE]
                            speed, steer, car_id, reward = struct.unpack('<BBif', header)

                            # Parse image
                            img_bytes = packet[HEADER_SIZE:]
                            img = np.frombuffer(img_bytes, dtype=np.uint8)
                            img = img.reshape((MERGED_HEIGHT, MERGED_WIDTH, 3))

                            print(f"Car {car_id} | Speed {speed} | Steer {steer} | Reward {reward}")
                            print(f"Image shape: {img.shape}")

                            # Remove processed packet from buffer
                            buffer = buffer[expected_packet_size:]

                    except ConnectionResetError:
                        print("Connection reset by Unity.")
                        break
                    except Exception as e:
                        print(f"Error receiving data: {e}")
                        break
