import os
import datetime
from stat import S_ISREG, ST_CTIME, ST_MODE
import glob

parent_loc = r"C:\Users\luoye\Desktop\stereo output\City01_Day"
meta_loc = os.path.join(parent_loc,"metadata")

meta_files = list(os.listdir(meta_loc))
meta_files_int = []
for metaf in meta_files:
    meta_files_int.append(int(metaf[:-4]))
meta_files_int.sort()

with open(os.path.join(meta_loc, f"{meta_files_int[0]:06d}.txt"),'r') as f:
    content = f.readlines()[0].split(" ")
    last_ts = datetime.datetime(2022, 3, 4, int(content[-4]), int(content[-3]), int(content[-2])).timestamp()
    last_ts_fake = last_ts

starting_value = 5e12
res = open(os.path.join(parent_loc,"time_cvt.txt"),'w')

date = datetime.date(2022, 3, 4)
prev_hrs, prev_mins, prev_sec = 0, 0, 0

for idx, item in enumerate(meta_files_int):
    item = f"{item:06d}.txt"
    with open(os.path.join(meta_loc, item), 'r') as f:
        if idx != 0:
            res.write("{}\n".format(last_ts + starting_value))
        content = f.readlines()[0].split(" ")
        hrs, mins, sec = content[-4], content[-3], content[-2]

        if int(hrs) < prev_hrs:
            date += datetime.timedelta(days=1)

        dt = datetime.datetime(date.year, date.month, date.day, int(hrs), int(mins), int(sec))
        prev_hrs, prev_mins, prev_sec = int(hrs), int(mins), int(sec)

        print(item, dt)
        this_ts = dt.timestamp()
        spent = (this_ts - last_ts_fake) * 0.033
        last_ts_fake = this_ts
        last_ts = last_ts + spent

res.write("{}\n".format(last_ts + starting_value))
res.close()

time_newformat = open(os.path.join(parent_loc,"time_cvt_orb.txt"),'w', newline='\n')
with open(os.path.join(parent_loc,"time_cvt.txt"),'r') as f:
    times = f.readlines()
    for single_entry in times:
        single_entry = float(single_entry[6:-1])
        time_newformat.write("{}\n".format(str(int(single_entry*1e6))))
        
time_newformat.close()
