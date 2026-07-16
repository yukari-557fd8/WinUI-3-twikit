import asyncio
import logging
from dataclasses import dataclass
from typing import Literal

import get_timeline_twikit as gtt
import twikit_client

logger = logging.getLogger(__name__)

ActionType = Literal["like", "unlike", "retweet", "reply", "quote"]


@dataclass
class ActionJob:
    action: ActionType
    tweet_id: str
    text: str | None = None


class ActionQueue:
    def __init__(self) -> None:
        self._queue: asyncio.Queue[ActionJob] = asyncio.Queue()
        self._worker_task: asyncio.Task | None = None

    async def enqueue(self, job: ActionJob) -> None:
        await self._queue.put(job)

    async def start_worker(self) -> None:
        if self._worker_task is None:
            self._worker_task = asyncio.create_task(self._worker_loop())
            logger.info("Action queue worker started")

    async def _worker_loop(self) -> None:
        while True:
            job = await self._queue.get()
            try:
                await self._execute(job)
            except Exception:
                logger.exception(
                    "Action queue worker failed: action=%s tweet_id=%s",
                    job.action,
                    job.tweet_id,
                )
            finally:
                self._queue.task_done()
                await asyncio.sleep(0.5)

    async def _execute(self, job: ActionJob) -> None:
        await twikit_client.login()

        match job.action:
            case "like":
                await gtt.client.favorite_tweet(job.tweet_id)
                logger.info("Queued like completed: tweet_id=%s", job.tweet_id)
            case "unlike":
                await gtt.client.unfavorite_tweet(job.tweet_id)
                logger.info("Queued unlike completed: tweet_id=%s", job.tweet_id)
            case "retweet":
                await gtt.client.retweet(job.tweet_id)
                logger.info("Queued retweet completed: tweet_id=%s", job.tweet_id)
            case "reply":
                await gtt.client.create_tweet(text=job.text or "", reply_to=job.tweet_id)
                logger.info("Queued reply completed: tweet_id=%s", job.tweet_id)
            case "quote":
                attachment_url = f"https://x.com/i/status/{job.tweet_id}"
                await gtt.client.create_tweet(
                    text=job.text or "",
                    attachment_url=attachment_url,
                )
                logger.info("Queued quote completed: tweet_id=%s", job.tweet_id)


action_queue = ActionQueue()