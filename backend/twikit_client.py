from twikit import Client
import os

client = Client("ja-JP")


async def login(cookies_file: str = "x.com.cookies_yukari_557fd8.json"):
    """中央集中ログイン関数"""
    if not os.path.exists(cookies_file):
        print("Cookiesファイルが見つかりません")
        return False

    try:
        client.load_cookies(cookies_file)
        # 初回のみ詳細表示（2回目以降は簡略化したい場合）
        if not hasattr(login, "logged_in"):
            print("クッキーロード完了（twikit_client）")
            login.logged_in = True
        return True
    except Exception as e:
        print(f"ログイン失敗: {e}")
        return False
