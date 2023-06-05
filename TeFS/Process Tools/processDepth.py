import cv2
import numpy as np
import math
import os
import glob
from matplotlib import cm

rows = 1080
cols = 1920
f_rows = 1080.0
f_cols = 1920.0
nc_z = 0.01
fc_z = 600.0
fov_v = 59

processLeft = False

# 
if processLeft:
    input_folder = r"C:\Users\luoye\Desktop\stereo output\City01_Day\gta0\cam0\depth"
    output_folder = r"C:\Users\luoye\Desktop\stereo output\City01_Day\gta0\depth_left_npy"
    output_png_folder = r"C:\Users\luoye\Desktop\stereo output\City01_Day\gta0\depth_left_color_png"

else:
    input_folder = r"C:\Users\luoye\Desktop\stereo output\City01_Day\gta0\cam1\depth"
    output_folder = r"C:\Users\luoye\Desktop\stereo output\City01_Day\gta0\depth_right_npy"
    output_png_folder = r"C:\Users\luoye\Desktop\stereo output\City01_Day\gta0\depth_right_color_png"

resize_shape = (1920, 1080)
# apply depth mask if needed.
truncate_depth = False
max_depth = 1000.0

def ndc_to_depth(ndc, map_xy):
    lower = ndc + (nc_z / (2 * fc_z)) * map_xy
    return map_xy / lower

def create_nc_xy_map(rows, cols, nc_z, fov_v):
    nc_h = 2 * nc_z * math.tan((fov_v * math.pi) / 360.0)
    nc_w = (cols / rows) * nc_h
    nc_xy_map = np.zeros((rows, cols))
    for j in range(rows):
        for i in range(cols):
            nc_x = abs(((2 * i) / (cols - 1.0)) - 1) * nc_w / 2.0
            nc_y = abs(((2 * j) / (rows - 1.0)) - 1) * nc_h / 2.0
            nc_xy_map[j, i] = math.sqrt(pow(nc_x, 2) + pow(nc_y, 2) + pow(nc_z, 2))
    return nc_xy_map

def process_bin_file(file_path, output_folder, output_png_folder, nc_xy_map):
    with open(file_path, 'rb') as fd:
        f = np.fromfile(fd, dtype=np.float32, count=rows * cols)
        im = f.reshape((rows, cols))

    depth_im_true = ndc_to_depth(im, nc_xy_map)
    
    if truncate_depth:
        depth_im_true[depth_im_true > max_depth] = max_depth

    depth_im_resized = cv2.resize(depth_im_true, resize_shape)

    file_basename = os.path.splitext(os.path.basename(file_path))[0]
    if processLeft:
        output_file = os.path.join(output_folder, f"{int(file_basename):06d}.npy")
        output_png_file = os.path.join(output_png_folder, f"{int(file_basename):06d}.png")
    else:
        output_file = os.path.join(output_folder, f"{int(file_basename):06d}.npy")
        output_png_file = os.path.join(output_png_folder, f"{int(file_basename):06d}.png")

    np.save(output_file, depth_im_resized)

    depth_im_norm = cv2.convertScaleAbs(depth_im_true)
    
    depth_im_color = cm.jet(depth_im_norm, bytes=True)
    cv2.imwrite(output_png_file, depth_im_color)

    print(f"Processed and saved: {output_file}, {output_png_file}")

nc_xy_map = create_nc_xy_map(rows, cols, nc_z, fov_v)

if not os.path.exists(output_folder):
    os.makedirs(output_folder)

if not os.path.exists(output_png_folder):
    os.makedirs(output_png_folder)

bin_files = glob.glob(os.path.join(input_folder, '*.bin'))

for file_path in bin_files:
    process_bin_file(file_path, output_folder, output_png_folder, nc_xy_map)

