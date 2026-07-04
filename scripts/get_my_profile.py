from twikit_client import client, login
from datetime import timezone, timedelta


async def get_own_profile():
    await login()  # 中央集中ログインを使用

    try:
        user = await client.user()

        created_str = "不明"
        try:
            if hasattr(user, "created_at_datetime") and user.created_at_datetime:
                dt = user.created_at_datetime.astimezone(timezone(timedelta(hours=9)))
                created_str = dt.strftime("%Y/%m/%d")
        except:
            created_str = getattr(user, "created_at", "不明")

        return {
            "id": user.id,
            "name": user.name,
            "screen_name": user.screen_name,
            "bio": user.description,
            "followers_count": user.followers_count,
            "following_count": user.following_count,
            "location": getattr(user, "location", None),
            "created_str": created_str,
            "profile_image_url": user.profile_image_url,
            "profile_banner_url": getattr(user, "profile_banner_url", None),
            "statuses_count": user.statuses_count,
            "favourites_count": user.favourites_count,
        }
    except Exception as e:
        print(f"プロフィール取得エラー: {e}")
        return {"error": str(e)}
