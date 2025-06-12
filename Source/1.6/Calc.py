import glob

lines = 0

exclude = [
  '\\Library\\',
  '\\obj\\'
]

# root_dir needs a trailing slash (i.e. /root/dir/)
for filename in glob.iglob('./**/*.cs', recursive=True):
  stop = False
  for ex in exclude:
    if (ex in filename):
      stop = True
      break
  if (stop):
    continue
  print(filename)
  with open (filename) as f:
    for i, _ in enumerate(f):
      pass
    lines += i


print ()

print ("Total lines: " + str(lines))
