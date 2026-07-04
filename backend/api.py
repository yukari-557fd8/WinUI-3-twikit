import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "scripts"))
sys.path.insert(0, str(ROOT / "backend"))

from pydantic import BaseModel
from fastapi.responses import FileResponse
from fastapi import FastAPI, UploadFile, File, Form
from typing import List

# 各機能モジュール
import twikit_client
import get_timeline_twikit as gtt
import twikit_test_load_cookies as ttl
import get_my_profile as gmp
import get_notifications_twikit as gnt
import get_search_twikit as gst

from paths import STATIC_DIR

# 安全な文字化け対策
try:
    if sys.stdout.encoding != "utf-8":
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if sys.stderr.encoding != "utf-8":
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
except:
    pass  # 失敗しても無視

app = FastAPI(title="Twikit API")


# --- 投稿 ---
class Input(BaseModel):
    text: str


# --- 投稿（修正版） ---
@app.post("/tweet")
async def tweet(text: str = Form(""), files: List[UploadFile] = File(default=[])):
    await twikit_client.login()
    tweet_id = await ttl.tweeting_with_media(text, files)
    return {"result": f"ツイート完了 ID: {tweet_id}"}


# --- タイムライン ---
@app.get("/timeline")
async def get_timeline(
    pages: int = 1,
    count: int | None = None,
    type: str = "for_you",
    cursor: str | None = None,
):
    await twikit_client.login()
    tweets = await gtt.get_timeline_tweets(
        pages=pages,
        count_per_page=count,
        timeline_type=type,
        cursor=cursor,
    )
    return tweets


# --- いいね ---
@app.post("/like/{tweet_id}")
async def like_tweet(tweet_id: str):
    await twikit_client.login()
    try:
        await gtt.client.favorite_tweet(tweet_id)
        return {"success": True, "action": "like"}
    except Exception as e:
        return {"success": False, "error": str(e)}


# --- いいね解除 ---
@app.delete("/like/{tweet_id}")
async def unlike_tweet(tweet_id: str):
    await twikit_client.login()
    try:
        await gtt.client.unfavorite_tweet(tweet_id)
        return {"success": True, "action": "unlike"}
    except Exception as e:
        return {"success": False, "error": str(e)}


# --- リツイート ---
@app.post("/retweet/{tweet_id}")
async def retweet_tweet(tweet_id: str):
    await twikit_client.login()
    try:
        await gtt.client.retweet(tweet_id)
        return {"success": True, "action": "retweet"}
    except Exception as e:
        return {"success": False, "error": str(e)}


# --- リプライ ---
@app.post("/reply/{tweet_id}")
async def reply_to_tweet(tweet_id: str, data: Input):
    await twikit_client.login()
    try:
        new_tweet = await gtt.client.create_tweet(text=data.text, reply_to=tweet_id)
        return {
            "success": True,
            "action": "reply",
            "tweet_id": tweet_id,
            "new_tweet_id": getattr(new_tweet, "id", None),
        }
    except Exception as e:
        return {"success": False, "error": str(e)}


# --- プロフィール ---
@app.get("/profile")
async def get_profile():
    await twikit_client.login()
    return await gmp.get_own_profile()


# --- 通知 ---
@app.get("/notifications")
async def get_notifications_endpoint(count: int = 20, refresh: bool = True):
    await twikit_client.login()
    return await gnt.get_notifications(count=count, refresh=refresh)


# --- 検索 ---
@app.get("/search")
async def search_tweets(
    query: str, count: int = 20, product: str = "Latest", cursor: str = None
):
    await twikit_client.login()
    try:
        tweets = await gst.search_tweets(
            query=query, count=count, product=product, cursor=cursor
        )
        return tweets
    except Exception as e:
        print(f"Search API Error: {e}")
        return {"error": str(e)}


# --- ヘルスチェック ---
@app.get("/")
async def root():
    return {"status": "ok"}


@app.get("/favicon.ico")
async def favicon():
    return FileResponse(STATIC_DIR / "favicon.ico")
