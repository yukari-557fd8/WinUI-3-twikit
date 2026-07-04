from twikit_client import client
from datetime import timezone, timedelta
from typing import List, Dict
import os
import json

last_cursor = None


async def login_if_needed(cookies_file: str = "x.com.cookies_yukari_557fd8.json"):
    if not os.path.exists(cookies_file):
        print("Cookiesファイルが見つかりません")
        return False
    try:
        with open(cookies_file, "r", encoding="utf-8") as f:
            cookies = json.load(f)
        client.set_cookies(cookies)
        print("Cookiesセット完了")
        return True
    except Exception as e:
        print(f"Cookiesセット失敗: {e}")
        return False


async def get_timeline_tweets(
    pages: int = 1,
    count_per_page: int | None = 30,
    timeline_type: str = "for_you",
    cursor: str | None = None,
) -> List[Dict]:
    global last_cursor
    await login_if_needed()

    results: List[Dict] = []
    current_cursor = cursor or None

    if current_cursor is None:
        last_cursor = None

    try:
        for i in range(pages):
            print(
                f"{i+1}ページ目取得中... (type: {timeline_type}, cursor: {'あり' if current_cursor else 'なし'})"
            )

            request_kwargs = {}
            if count_per_page is not None:
                request_kwargs["count"] = count_per_page
            if current_cursor:
                request_kwargs["cursor"] = current_cursor

            if timeline_type == "latest":
                # 最新タイムライン
                timeline = await client.get_latest_timeline(**request_kwargs)
            else:
                # おすすめ（For You）タイムライン
                timeline = await client.get_timeline(**request_kwargs)

            if not timeline or len(timeline) == 0:
                print("これ以上取得できません")
                break

            print(f"  └─ 取得したツイート数: {len(timeline)}")

            seen = set()

            for t in timeline:
                try:
                    dt = t.created_at_datetime.astimezone(timezone(timedelta(hours=9)))
                    created_str = dt.strftime("%Y/%m/%d %H:%M:%S")
                except:
                    created_str = getattr(t, "created_at", "不明")

                # メディア抽出 (画像 + 動画対応)
                media_items = []
                try:
                    if hasattr(t, "_data"):
                        legacy = t._data.get("legacy", {})
                        for m in legacy.get("extended_entities", {}).get("media", []):
                            media_type = m.get("type", "photo")
                            if media_type == "photo":
                                url = m.get("media_url_https") or m.get("media_url")
                                if url:
                                    media_items.append({"type": "image", "url": url})
                            elif media_type in ["video", "animated_gif"]:
                                # 動画の場合、最高品質のvariantを探す
                                video_info = m.get("video_info", {})
                                variants = video_info.get("variants", [])
                                if variants:
                                    best_variant = max(
                                        (v for v in variants if v.get("content_type") == "video/mp4"),
                                        key=lambda v: v.get("bitrate", 0),
                                        default=variants[0],
                                    )
                                    url = best_variant.get("url")
                                    if url:
                                        media_items.append(
                                            {
                                                "type": "video",
                                                "url": url,
                                                "thumbnail": m.get("media_url_https") or m.get("media_url"),
                                            }
                                        )
                                else:
                                    url = m.get("media_url_https") or m.get("media_url")
                                    if url:
                                        media_items.append({"type": "video", "url": url})
                except Exception as media_err:
                    print(f"メディア抽出エラー: {media_err}")
                    pass

                if t.id in seen:
                    continue
                seen.add(t.id)
                results.append(
                    {
                        "id": t.id,
                        "text": t.text or "",
                        "created_at": created_str,
                        "user_name": getattr(t.user, "name", "Unknown"),
                        "user_screen_name": getattr(t.user, "screen_name", ""),
                        "user_profile_image": getattr(t.user, "profile_image_url", "") or "",
                        "favorite_count": getattr(t, "favorite_count", 0),
                        "retweet_count": getattr(t, "retweet_count", 0),
                        "media_items": media_items,
                        "reply_count": getattr(t, "reply_count", 0),
                    }
                )

            # cursor更新
            if hasattr(timeline, "next_cursor") and timeline.next_cursor:
                current_cursor = timeline.next_cursor
                last_cursor = current_cursor
                print(f"next_cursor 更新: {current_cursor[:50]}...")
            else:
                print("next_cursor がありません")
                break

    except Exception as e:
        print(f"エラー: {e}")

    print(f"今回合計 {len(results)} 件取得")
    return {"tweets": results, "next_cursor": current_cursor}
