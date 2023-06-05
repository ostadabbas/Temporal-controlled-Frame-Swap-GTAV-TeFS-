import os

timestamp_file = r"C:\Users\luoye\Desktop\stereo output\City01_Day\time_cvt_orb.txt"


left_folder = r"C:\Users\luoye\Desktop\stereo output\City01_Day\gta0\cam0\data"
right_folder =r"C:\Users\luoye\Desktop\stereo output\City01_Day\gta0\cam1\data"



with open(timestamp_file, 'r') as f:
    timestamps = f.read().splitlines()

left_images = os.listdir(left_folder)
left_images = [f for f in left_images if f.endswith('.png')]
left_images = sorted(left_images, key=lambda x: int(x.split('.')[0]))

for img in left_images:
    print(img)

for i, filename in enumerate(left_images):
    old_name = os.path.join(left_folder, filename)
    new_name = os.path.join(left_folder, timestamps[i]+'.png')
    os.rename(old_name, new_name)

right_images = os.listdir(right_folder)
right_images = [f for f in right_images if f.endswith('.png')]
right_images = sorted(right_images, key=lambda x: int(x.split('.')[0]))

for img in right_images:
    print(img)

for i, filename in enumerate(right_images):
    old_name = os.path.join(right_folder, filename)
    new_name = os.path.join(right_folder, timestamps[i]+'.png')
    os.rename(old_name, new_name)


