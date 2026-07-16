from twikit_client import client, login
from datetime import timezone, timedelta, datetime
from typing import List, Dict

from tweet_serializer import _normalize_text

# 状態をモジュールレベルで保持（ただし毎回リセット推奨）
_notifications_result = None


async def get_notifications(count: int = 20, refresh: bool = True) -> List[Dict]:
    global _notifications_result
    login()

    results: List[Dict] = []

    try:
        if refresh or _notifications_result is None:
            # 初回 or リフレッシュ時は最新から取得
            _notifications_result = await client.get_notifications("All", count=count)
            print("✅ 通知: 最新から取得")
        else:
            # refresh=False の場合のみ続きを取得（必要なら後で使う）
            try:
                _notifications_result = await _notifications_result.next()
                print(".next() で追加取得")
            except:
                print("これ以上通知はありません")
                _notifications_result = None
                return results

        if not _notifications_result or len(_notifications_result) == 0:
            print("通知はありません")
            _notifications_result = None
            return results

        for n in _notifications_result:
            created_str = "不明"
            try:
                if hasattr(n, "timestamp_ms") and n.timestamp_ms:
                    dt = datetime.fromtimestamp(n.timestamp_ms / 1000, tz=timezone.utc)
                    dt_jst = dt.astimezone(timezone(timedelta(hours=9)))
                    created_str = dt_jst.strftime("%Y/%m/%d %H:%M:%S")
            except:
                pass

            actor = getattr(n, "user", None) or getattr(n, "from_user", None)
            target_tweet = getattr(n, "tweet", None) or getattr(n, "target", None)

            results.append(
                {
                    "id": getattr(n, "id", ""),
                    "type": getattr(n, "type", "unknown"),
                    "text": _normalize_text(
                        getattr(n, "text", "") or getattr(n, "message", "")
                    ),
                    "actor_name": (
                        getattr(actor, "name", "Unknown") if actor else "Unknown"
                    ),
                    "actor_screen_name": (
                        getattr(actor, "screen_name", "") if actor else ""
                    ),
                    "actor_profile_image": (
                        getattr(actor, "profile_image_url", "") if actor else ""
                    ),
                    "created_at": created_str,
                    "target_tweet_text": _normalize_text(
                        getattr(target_tweet, "text", "") if target_tweet else ""
                    ),
                }
            )

        print(f"Notifications 取得: {len(results)} 件")

    except Exception as e:
        print(f"Notifications取得エラー: {e}")
        _notifications_result = None

    return results
