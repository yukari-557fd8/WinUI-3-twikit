from typing import Dict, List

from tweet_serializer import tweet_to_dict
from twikit_client import client, login

last_search_cursor = None
current_query = None
seen_search_tweets = set()


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
            seen_search_tweets.clear()
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
            results.append(tweet_to_dict(t))

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