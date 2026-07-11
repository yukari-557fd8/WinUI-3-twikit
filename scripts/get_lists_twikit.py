from typing import Dict, List

from tweet_serializer import tweet_to_dict
from twikit_client import client, login


async def get_user_lists(count: int = 100, cursor: str | None = None) -> Dict:
    await login()

    try:
        print(f"リスト一覧取得中... count={count} cursor={'あり' if cursor else 'なし'}")
        result = await client.get_lists(count=count, cursor=cursor)

        lists: List[Dict] = []
        for lst in result:
            lists.append(
                {
                    "id": lst.id,
                    "name": lst.name or "",
                    "description": lst.description or "",
                    "mode": lst.mode,
                    "member_count": lst.member_count,
                    "subscriber_count": lst.subscriber_count,
                }
            )

        next_cursor = getattr(result, "next_cursor", None)
        print(f"リスト {len(lists)} 件取得")
        return {"lists": lists, "next_cursor": next_cursor}
    except Exception as e:
        print(f"リスト一覧取得エラー: {e}")
        return {"lists": [], "next_cursor": None}


async def get_list_timeline(
    list_id: str, count: int = 30, cursor: str | None = None
) -> Dict:
    await login()

    results: List[Dict] = []
    next_cursor = cursor

    try:
        print(
            f"リストタイムライン取得中... list_id={list_id} "
            f"count={count} cursor={'あり' if cursor else 'なし'}"
        )
        timeline = await client.get_list_tweets(list_id, count=count, cursor=cursor)

        if not timeline:
            print("タイムラインが空です")
            return {"tweets": [], "next_cursor": None}

        seen = set()
        for t in timeline:
            if t.id in seen:
                continue
            seen.add(t.id)
            results.append(tweet_to_dict(t))

        if hasattr(timeline, "next_cursor") and timeline.next_cursor:
            next_cursor = timeline.next_cursor
            print(f"next_cursor 更新: {next_cursor[:50]}...")
        else:
            next_cursor = None

    except Exception as e:
        print(f"リストタイムライン取得エラー: {e}")

    print(f"リストタイムライン {len(results)} 件取得")
    return {"tweets": results, "next_cursor": next_cursor}