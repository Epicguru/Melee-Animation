import glob

# root_dir needs a trailing slash (i.e. /root/dir/)
for filename in glob.iglob('./**/*.cs', recursive=True):
     print(filename)
