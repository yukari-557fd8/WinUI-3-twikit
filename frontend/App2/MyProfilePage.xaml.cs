using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
namespace App2
{
    public sealed partial class MyProfilePage : Page
    {
        public MyProfilePage()
        {
            this.InitializeComponent();
            Loaded += MyProfilePage_Loaded;
        }

        private async void MyProfilePage_Loaded(object sender, RoutedEventArgs e)
        {
            App.MainWindow?.ShowLoading(true);   // ← 追加
            await LoadProfileAsync();
            App.MainWindow?.ShowLoading(false);  // ← 追加
        }

        private async Task LoadProfileAsync()
        {
            try
            {
                // Python側からプロフィール取得（あなたの連携方法に合わせて調整）
                var profile = await GetProfileFromPythonAsync();

                if (profile.ContainsKey("error"))
                {
                    DisplayNameText.Text = "エラー";
                    BioText.Text = profile["error"].ToString();
                    return;
                }

                // 基本情報
                DisplayNameText.Text = profile["name"]?.ToString() ?? "名前なし";
                HandleText.Text = $"@{profile["screen_name"]}";
                BioText.Text = profile["bio"]?.ToString() ?? "自己紹介文がありません";

                // 統計
                FollowingCount.Text = profile["following_count"]?.ToString() ?? "0";
                FollowersCount.Text = profile["followers_count"]?.ToString() ?? "0";
                TweetsCount.Text = profile.GetValueOrDefault("statuses_count", "0").ToString();

                // その他
                LocationText.Text = $"📍 {profile["location"] ?? "未設定"}";
                JoinedText.Text = $"登録日: {profile["created_str"]?.ToString() ?? "不明"}";

                // プロフィール画像
                var profileImageUrl = profile.GetValueOrDefault("profile_image_url")?.ToString();
                if (!string.IsNullOrEmpty(profileImageUrl))
                {
                    ProfilePicture.ProfilePicture = new BitmapImage(new Uri(profileImageUrl));
                }

                // バナー画像
                var bannerUrl = profile.GetValueOrDefault("profile_banner_url")?.ToString();
                if (!string.IsNullOrEmpty(bannerUrl))
                {
                    BannerImage.Source = new BitmapImage(new Uri(bannerUrl));
                }
            }
            catch (Exception ex)
            {
                BioText.Text = $"読み込みエラー: {ex.Message}";
            }
        }

        /// Python (Twikit) から自身のプロフィールを取得
        private async Task<Dictionary<string, object>> GetProfileFromPythonAsync()
        {
            try
            {
                var client = new HttpClient();
                var response = await client.GetAsync("http://localhost:8000/profile");
                response.EnsureSuccessStatusCode();

                var profile = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
                return profile ?? new Dictionary<string, object> { ["error"] = "空のレスポンス" };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { ["error"] = ex.Message };
            }
        }
    }
}