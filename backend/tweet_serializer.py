from datetime import timezone, timedelta
from typing import Any, Dict, List


def _format_created_at(t) -> str:
    try:
        dt = t.created_at_datetime.astimezone(timezone(timedelta(hours=9)))
        return dt.strftime("%Y/%m/%d %H:%M:%S")
    except Exception:
        return getattr(t, "created_at", "不明")


def _append_media_entry(media_items: List[Dict[str, str]], seen_urls: set[str], m: Dict[str, Any]) -> None:
    media_type = m.get("type", "photo")
    if media_type == "photo":
        url = m.get("media_url_https") or m.get("media_url")
        if url and url not in seen_urls:
            seen_urls.add(url)
            media_items.append({"type": "image", "url": url})
    elif media_type in ["video", "animated_gif"]:
        video_info = m.get("video_info", {})
        variants = video_info.get("variants", [])
        video_url = None
        if variants:
            best_variant = max(
                (v for v in variants if v.get("content_type") == "video/mp4"),
                key=lambda v: v.get("bitrate", 0),
                default=variants[0],
            )
            video_url = best_variant.get("url")
        thumb = m.get("media_url_https") or m.get("media_url")
        if not video_url:
            video_url = thumb
        if video_url and video_url not in seen_urls:
            seen_urls.add(video_url)
            item: Dict[str, str] = {
                "type": "animated_gif" if media_type == "animated_gif" else "video",
                "url": video_url,
            }
            if thumb and thumb != video_url:
                item["thumbnail"] = thumb
            elif thumb:
                item["thumbnail"] = thumb
            media_items.append(item)


def _extract_media(t) -> List[Dict[str, str]]:
    media_items: List[Dict[str, str]] = []
    seen_urls: set[str] = set()
    try:
        legacy: Dict[str, Any] = {}
        if hasattr(t, "_legacy"):
            legacy = t._legacy
        elif hasattr(t, "_data"):
            legacy = t._data.get("legacy", {})

        for m in legacy.get("extended_entities", {}).get("media", []):
            _append_media_entry(media_items, seen_urls, m)
        for m in legacy.get("entities", {}).get("media", []):
            _append_media_entry(media_items, seen_urls, m)

        if not media_items and hasattr(t, "media"):
            for media_obj in t.media or []:
                media_type = getattr(media_obj, "type", "photo")
                if media_type == "photo":
                    url = getattr(media_obj, "media_url", None)
                    if url and url not in seen_urls:
                        seen_urls.add(url)
                        media_items.append({"type": "image", "url": url})
                elif media_type in ["video", "animated_gif"]:
                    streams = getattr(media_obj, "streams", None) or []
                    video_url = streams[0].url if streams else getattr(media_obj, "media_url", None)
                    thumb = getattr(media_obj, "media_url", None)
                    if video_url and video_url not in seen_urls:
                        seen_urls.add(video_url)
                        item = {"type": "video", "url": video_url}
                        if thumb:
                            item["thumbnail"] = thumb
                        media_items.append(item)
    except Exception as media_err:
        print(f"メディア抽出エラー: {media_err}")

    return media_items


def _quote_to_dict(quoted) -> Dict[str, Any]:
    text = ""
    if hasattr(quoted, "full_text"):
        text = quoted.full_text or ""
    if not text:
        text = getattr(quoted, "text", "") or ""

    return {
        "id": quoted.id,
        "text": text,
        "created_at": _format_created_at(quoted),
        "user_name": getattr(quoted.user, "name", "Unknown"),
        "user_screen_name": getattr(quoted.user, "screen_name", ""),
        "user_profile_image": getattr(quoted.user, "profile_image_url", "") or "",
        "media_items": _extract_media(quoted),
        "is_unavailable": False,
    }


def tweet_to_dict(t) -> Dict[str, Any]:
    retweeted = getattr(t, "retweeted_tweet", None)
    display = retweeted if retweeted else t
    display_legacy = display._legacy if hasattr(display, "_legacy") else {}

    text = ""
    if hasattr(display, "full_text"):
        text = display.full_text or ""
    if not text:
        text = getattr(display, "text", "") or ""

    result = {
        "id": display.id,
        "timeline_entry_id": str(t.id),
        "text": text,
        "created_at": _format_created_at(display),
        "user_name": getattr(display.user, "name", "Unknown"),
        "user_screen_name": getattr(display.user, "screen_name", ""),
        "user_profile_image": getattr(display.user, "profile_image_url", "") or "",
        "favorite_count": getattr(display, "favorite_count", 0),
        "retweet_count": getattr(display, "retweet_count", 0),
        "reply_count": getattr(display, "reply_count", 0),
        "media_items": _extract_media(display),
        "is_liked": display_legacy.get("favorited", False),
        "is_retweeted": display_legacy.get("retweeted", False),
        "is_retweet": retweeted is not None,
        "retweeted_by_name": getattr(t.user, "name", None) if retweeted else None,
        "retweeted_by_screen_name": getattr(t.user, "screen_name", None) if retweeted else None,
    }

    if getattr(display, "is_quote_status", False):
        quoted = getattr(display, "quote", None)
        if quoted:
            result["quoted_tweet"] = _quote_to_dict(quoted)
        else:
            result["quoted_tweet"] = {"is_unavailable": True}

    return result