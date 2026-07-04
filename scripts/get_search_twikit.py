from twikit_client import client, login
from datetime import timezone, timedelta
from typing import List, Dict

last_search_cursor = None
current_query = None
seen_search_tweets = set()   # ← グローバルで永続的に重複防止


async def search_tweets(
    query: str, count: int = 20, product: str = "Latest", cursor: str = None
) -> List[Dict]:
    global last_search_cursor, current_query, seen_search_tweets

    await login()

    results: List[Dict] = []

    try:
        if current_query != query:
            last_search_cursor = None
            current_query = query
            seen_search_tweets.clear()   # 新しいクエリではリセット
            print(f"新しい検索クエリ: {query} → seenクリア")

        print(f"検索取得中... query='{query}' cursor={'あり' if cursor or last_search_cursor else 'なし'}")

        use_cursor = cursor or last_search_cursor
        search_result = await client.search_tweet(
            query, product=product, count=count, cursor=use_cursor
        )

        if not search_result or len(search_result) == 0:
            print("これ以上検索結果はありません")
            last_search_cursor = None
            return results

        print(f"  └─ 取得したツイート数: {len(search_result)}")

        for t in search_result:
            if t.id in seen_search_tweets:
                continue
            seen_search_tweets.add(t.id)

            try:
                dt = t.created_at_datetime.astimezone(timezone(timedelta(hours=9)))
                created_str = dt.strftime("%Y/%m/%d %H:%M:%S")
            except:
                created_str = getattr(t, "created_at", "不明")

            media_urls = []
            try:
                if hasattr(t, "_data"):
                    legacy = t._data.get("legacy", {})
                    for m in legacy.get("extended_entities", {}).get("media", []):
                        url = m.get("media_url_https") or m.get("media_url")
                        if url and url not in media_urls:
                            media_urls.append(url)
            except:
                pass

            results.append({
                "id": t.id,
                "text": t.text or "",
                "created_at": created_str,
                "user_name": getattr(t.user, "name", "Unknown"),
                "user_screen_name": getattr(t.user, "screen_name", ""),
                "user_profile_image": getattr(t.user, "profile_image_url", "") or "",
                "favorite_count": getattr(t, "favorite_count", 0),
                "retweet_count": getattr(t, "retweet_count", 0),
                "media_urls": media_urls,
                "reply_count": getattr(t, "reply_count", 0),
            })

        # カーソル更新
        if hasattr(search_result, "next_cursor") and search_result.next_cursor:
            last_search_cursor = search_result.next_cursor
            print(f"next_cursor 更新: {last_search_cursor[:50]}...")
        else:
            last_search_cursor = None
            print("これ以上結果なし")

    except Exception as e:
        print(f"検索エラー: {e}")
        last_search_cursor = None

    print(f"検索完了: {len(results)} 件")
    return results