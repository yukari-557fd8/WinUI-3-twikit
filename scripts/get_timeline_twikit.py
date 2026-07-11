from typing import Dict, List

from tweet_serializer import tweet_to_dict
from twikit_client import client, login

last_cursor = None


async def get_timeline_tweets(
    pages: int = 1,
    count_per_page: int | None = 30,
    timeline_type: str = "for_you",
    cursor: str | None = None,
) -> Dict:
    global last_cursor
    await login()

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
                timeline = await client.get_latest_timeline(**request_kwargs)
            else:
                timeline = await client.get_timeline(**request_kwargs)

            if not timeline or len(timeline) == 0:
                print("これ以上取得できません")
                break

            print(f"  └─ 取得したツイート数: {len(timeline)}")

            seen = set()

            for t in timeline:
                if t.id in seen:
                    continue
                seen.add(t.id)
                results.append(tweet_to_dict(t))

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