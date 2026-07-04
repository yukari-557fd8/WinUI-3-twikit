using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace App2
{
    public sealed partial class SettingsPage : Page
    {
        private readonly string jsonPath =
            @"C:\Users\user\Documents\visual_studio\test_app_with_menu\App2\x.com.cookies_yukari_557fd8.json";

        public SettingsPage()
        {
            this.InitializeComponent();

            LoadJsonValues();
        }

        private void LoadJsonValues()
        {
            if (!File.Exists(jsonPath))
            {
                return; // ファイルが無ければ何もしない
            }

            var json = File.ReadAllText(jsonPath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

            if (dict == null) return;

            // TextBox に初期値をセット
            if (dict.TryGetValue("auth_token", out var auth))
            {
                TextBoxA.Text = auth;
            }

            if (dict.TryGetValue("ct0", out var ct0))
            {
                TextBoxB.Text = ct0;
            }
        }

        private void OnApplyClick(object sender, RoutedEventArgs e)
        {
            // 保存処理（前回のコードと同じ）
            var dict = new Dictionary<string, string>
            {
                ["auth_token"] = TextBoxA.Text,
                ["ct0"] = TextBoxB.Text
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(dict, options));
        }
    }
}
