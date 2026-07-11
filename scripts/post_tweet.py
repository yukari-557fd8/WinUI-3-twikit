import os
from typing import List

from fastapi import UploadFile

from twikit_client import client, login


async def tweeting_with_media(text: str, files: List[UploadFile] = None):
    await login()

    media_ids = []
    if files:
        for file in files:
            if file.filename:
                content = await file.read()
                temp_path = f"temp_{file.filename}"
                with open(temp_path, "wb") as f:
                    f.write(content)

                media_id = await client.upload_media(temp_path)
                media_ids.append(media_id)
                os.remove(temp_path)

    tweet = await client.create_tweet(text=text, media_ids=media_ids)
    print(f"投稿完了  ID: {tweet.id} (メディア: {len(media_ids)}件)")
    return tweet.id