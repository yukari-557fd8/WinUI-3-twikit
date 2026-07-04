from pathlib import Path
import os

REPO_ROOT = Path(__file__).resolve().parent.parent
DATA_DIR = REPO_ROOT / "data"
DEFAULT_COOKIES_FILE = DATA_DIR / "cookies.json"
STATIC_DIR = REPO_ROOT / "static"


def get_cookies_file() -> str:
    return os.environ.get("COOKIES_FILE", str(DEFAULT_COOKIES_FILE))