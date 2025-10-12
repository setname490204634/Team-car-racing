import socket
import struct

HOST = "127.0.0.1"        # Unity is running locally
CONTROLPORT = 5005        # must match controlPort in Unity
CARINSTRUCTIONPORT = 5006 # must match carInstructionsPort in Unity

def send_command(command_byte: int, value_byte: int):
    """
    Send a 2-byte command packet to Unity control server.
    - command_byte: main command (0–255)
    - value_byte: associated value (0–255)
    """
    try:
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
            s.connect((HOST, CONTROLPORT))
            
            # Pack two bytes into a packet
            packet = struct.pack('BB', command_byte & 0xFF, value_byte & 0xFF)
            s.sendall(packet)
            
    except ConnectionRefusedError:
        print("Could not connect to Unity control server.")
    except Exception as e:
        print(f"Error: {e}")

def send_car_instruction(car_index: int, steering: int, throttle: int):
    """
    Send driving instructions to Unity car instructions server.
    - car_index: 32-bit integer car ID
    - steering: 0–255
    - throttle: 0–255
    """
    try:
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
            s.connect((HOST, CARINSTRUCTIONPORT))

            # Pack 32-bit int + 2 bytes (little-endian)
            packet = struct.pack('<I2B', car_index, steering, throttle)
            s.sendall(packet)

    except ConnectionRefusedError:
        print("Could not connect to Unity car instructions server.")
    except Exception as e:
        print(f"Error: {e}")
