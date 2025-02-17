import os

def print_tree(path, prefix=""):
    try:
        with os.scandir(path) as it:
            entries = sorted(it, key=lambda e: e.name.lower())
            entries_count = len(entries)
            for index, entry in enumerate(entries):
                is_last = index == entries_count - 1
                connector = "└── " if is_last else "├── "
                print(prefix + connector + entry.name)
                if entry.is_dir():
                    extension = "    " if is_last else "│   "
                    print_tree(entry.path, prefix + extension)
    except PermissionError:
        print(prefix + "└── " + "[Permission Denied]")

if __name__ == "__main__":
    root_folder = "Assets"  # Ensures it only lists the folders/files in the Assets folder
    print(root_folder)
    print_tree(root_folder)