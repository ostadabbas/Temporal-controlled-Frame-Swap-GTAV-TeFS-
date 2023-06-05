import os
import numpy as np
from scipy.spatial.transform import Rotation as R

folder = r"C:\Users\luoye\Desktop\stereo output\City01_Day\metadata"
time_file = r"C:\Users\luoye\Desktop\stereo output\City01_Day\time_cvt_orb.txt"
out_path = r"C:\Users\luoye\Desktop\stereo output\City01_Day\gt.txt"

poseNumber = len([entry for entry in os.scandir(folder) if entry.is_file() and entry.name.endswith('.txt')])
offset = 0

timestamps = []
with open(time_file, "r") as fin:
    for line in fin:
        timestamps.append(int(line.strip()))

with open(out_path, "w") as fout:
    for i in range(poseNumber):
        meta_file = os.path.join(folder, f"{i+offset:06d}.txt")
        with open(meta_file, "r") as fin:
            line = fin.readline()
            cam2_pos = line.split("cam1 position: ")[1].split(" ")[:3]
            cam2_x, cam2_y, cam2_z = map(float, cam2_pos)
            
            cam2_rot = line.split("cam1 rotation: ")[1].split(" ")[:3]
            roll, pitch, yaw = map(float, cam2_rot)
            r = R.from_euler('xyz', [roll, pitch, yaw], degrees=True)
            quaternion = r.as_quat()
            
            timestamp = timestamps[i]
            fout.write("{} {} {} {} {} {} {} {}\n".format(timestamp, cam2_x, cam2_y, cam2_z, quaternion[0], quaternion[1], quaternion[2], quaternion[3]))
