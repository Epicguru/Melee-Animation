import glob

lines = 0

# root_dir needs a trailing slash (i.e. /root/dir/)
for filename in glob.iglob('./**/*.cs', recursive=True):
     print(filename)
     with open (filename) as f:
          for i, _ in enumerate(f):
            pass
          lines += i


print ()

print ("Total lines: " + str(lines))
