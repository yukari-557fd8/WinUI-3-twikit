from twikit_client import client, login
import os
from fastapi import UploadFile
from typing import List


async def tweeting_with_media(text: str, files: List[UploadFile] = None):
    login()

    media_ids = []
    if files:
        for file in files:
            if file.filename:  # 空ファイル防止
                content = await file.read()
                # 一時ファイルとして保存（twikitのupload_mediaがファイルパスを期待する場合）
                temp_path = f"temp_{file.filename}"
                with open(temp_path, "wb") as f:
                    f.write(content)

                media_id = await client.upload_media(temp_path)
                media_ids.append(media_id)
                os.remove(temp_path)  # 後片付け

    tweet = await client.create_tweet(text=text, media_ids=media_ids)
    print(f"投稿完了  ID: {tweet.id} (メディア: {len(media_ids)}件)")
    return tweet.id
