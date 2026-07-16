import sys
from contextlib import asynccontextmanager
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "scripts"))
sys.path.insert(0, str(ROOT / "backend"))

from pydantic import BaseModel
from fastapi.responses import FileResponse
from fastapi import FastAPI, UploadFile, File, Form
from typing import List

import twikit_client
import post_tweet as pt
import get_timeline_twikit as gtt
import get_my_profile as gmp
import get_notifications_twikit as gnt
import get_search_twikit as gst
import get_lists_twikit as glt
from action_queue import ActionJob, action_queue
from paths import STATIC_DIR

try:
    if sys.stdout.encoding != "utf-8":
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if sys.stderr.encoding != "utf-8":
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass


@asynccontextmanager
async def lifespan(app: FastAPI):
    await action_queue.start_worker()
    yield


app = FastAPI(title="Twikit API", lifespan=lifespan)


class Input(BaseModel):
    text: str


@app.post("/tweet")
async def tweet(text: str = Form(""), files: List[UploadFile] = File(default=[])):
    await twikit_client.login()
    tweet_id = await pt.tweeting_with_media(text, files)
    return {"result": f"ツイート完了 ID: {tweet_id}"}


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


@app.post("/like/{tweet_id}")
async def like_tweet(tweet_id: str):
    await action_queue.enqueue(ActionJob(action="like", tweet_id=tweet_id))
    return {"success": True, "action": "like", "queued": True}


@app.delete("/like/{tweet_id}")
async def unlike_tweet(tweet_id: str):
    await action_queue.enqueue(ActionJob(action="unlike", tweet_id=tweet_id))
    return {"success": True, "action": "unlike", "queued": True}


@app.post("/retweet/{tweet_id}")
async def retweet_tweet(tweet_id: str):
    await action_queue.enqueue(ActionJob(action="retweet", tweet_id=tweet_id))
    return {"success": True, "action": "retweet", "queued": True}


@app.post("/reply/{tweet_id}")
async def reply_to_tweet(tweet_id: str, data: Input):
    await twikit_client.login()
    tweet = await gtt.client.create_tweet(
        text=data.text or "",
        reply_to=tweet_id,
    )
    return {
        "success": True,
        "action": "reply",
        "tweet_id": tweet_id,
        "new_tweet_id": str(tweet.id),
        "queued": False,
    }


@app.post("/quote/{tweet_id}")
async def quote_tweet(tweet_id: str, data: Input):
    await twikit_client.login()
    attachment_url = f"https://x.com/i/status/{tweet_id}"
    tweet = await gtt.client.create_tweet(
        text=data.text or "",
        attachment_url=attachment_url,
    )
    return {
        "success": True,
        "action": "quote",
        "tweet_id": tweet_id,
        "new_tweet_id": str(tweet.id),
        "queued": False,
    }


@app.get("/profile")
async def get_profile():
    await twikit_client.login()
    return await gmp.get_own_profile()


@app.get("/notifications")
async def get_notifications_endpoint(count: int = 20, refresh: bool = True):
    await twikit_client.login()
    return await gnt.get_notifications(count=count, refresh=refresh)


@app.get("/lists")
async def get_lists_endpoint(count: int = 100, cursor: str | None = None):
    await twikit_client.login()
    try:
        return await glt.get_user_lists(count=count, cursor=cursor)
    except Exception as e:
        print(f"Lists API Error: {e}")
        return {"error": str(e), "lists": [], "next_cursor": None}


@app.get("/lists/{list_id}/tweets")
async def get_list_tweets_endpoint(
    list_id: str, count: int = 30, cursor: str | None = None
):
    await twikit_client.login()
    try:
        return await glt.get_list_timeline(list_id=list_id, count=count, cursor=cursor)
    except Exception as e:
        print(f"List Tweets API Error: {e}")
        return {"error": str(e), "tweets": [], "next_cursor": None}


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


@app.get("/")
async def root():
    return {"status": "ok"}


@app.get("/favicon.ico")
async def favicon():
    return FileResponse(STATIC_DIR / "favicon.ico")