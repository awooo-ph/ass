﻿using System;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Support.V7.App;
using Android.Views;
using Android.Widget;
using norsu.ass.Network;

namespace norsu.ass
{
    [Activity(Icon = "@drawable/ic_launcher", Label = "Sign In", Theme = "@style/AppTheme",
        ScreenOrientation = ScreenOrientation.Portrait,
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation)]
    public class LoginActivity : AppCompatActivity
    {
        private Button _loginButton;
        private Button _register;
        private RelativeLayout _progressView;
        private LinearLayout _userView;
        private EditText _username;
        private EditText _password;
        private EditText _nickName;
        private CheckBox _anonymous;
        
        
        protected override void OnCreate(Bundle savedInstanceState)
        {
            var dlg = new Android.Support.V7.App.AlertDialog.Builder(this);
            if (Client.Server == null)
            {
                dlg.SetTitle("Connection to server is not established.");
                dlg.SetMessage("Please make sure you are connected to the server and try again.");
                dlg.SetNegativeButton("Exit", (sender, args) =>
                {
                    FinishAffinity();
                });
                dlg.Show();
                return;
            }
            Messenger.Default.AddListener(Messages.Shutdown, () =>
            {
                RunOnUiThread(() =>
                {
                    dlg = new Android.Support.V7.App.AlertDialog.Builder(this);
                    dlg.SetMessage("Disconnected from server.");
                    dlg.SetMessage("The server has shutdown. Please try again later.");
                    dlg.SetPositiveButton("EXIT", (sender, args) =>
                    {
                        FinishAffinity();
                    });
                    dlg.SetCancelable(false);
                    dlg.Show();
                });
            });

            base.OnCreate(savedInstanceState);

            // Create your application here
            SetContentView(Resource.Layout.Login);

            _loginButton = FindViewById<Button>(Resource.Id.login);
            _register = FindViewById<Button>(Resource.Id.register);
            _anonymous = FindViewById<CheckBox>(Resource.Id.anonymous);
            _userView = FindViewById<LinearLayout>(Resource.Id.userView);
            _progressView = FindViewById<RelativeLayout>(Resource.Id.progress);
            _nickName = FindViewById<EditText>(Resource.Id.nickName);
            _username = FindViewById<EditText>(Resource.Id.userName);
            _password = FindViewById<EditText>(Resource.Id.password);
            
            _loginButton.Click += LoginButtonOnClick;
            _anonymous.CheckedChange += AnonymousOnCheckedChange;

            if (Client.Server != null)
            {
                _anonymous.Visibility = Client.Server.AllowAnnonymous ? ViewStates.Visible : ViewStates.Gone;
                _register.Visibility = Client.Server.AllowRegistration ? ViewStates.Visible : ViewStates.Gone;
                _register.Click += RegisterOnClick;
                
            }
           
        }
        
        private void RegisterOnClick(object sender, EventArgs eventArgs)
        {
            StartActivity(typeof(RegisterActivity));
        }

        public override bool OnNavigateUp()
        {
            MainActivity.CurrentScreen = Screens.Login;
            return base.OnNavigateUp();
        }

        protected override void OnSaveInstanceState(Bundle outState)
        {
            outState.PutString("login_username", _username.Text);
            outState.PutString("login_nick",_nickName.Text);
            outState.PutString("login_password",_username.Text);
            outState.PutBoolean("login_anonymous",_anonymous.Checked);
            base.OnSaveInstanceState(outState);
        }

        protected override void OnRestoreInstanceState(Bundle s)
        {
            base.OnRestoreInstanceState(s);
            if (s == null) return;
            _username.Text = s.GetString("login_username");
            _password.Text = s.GetString("login_password");
            _nickName.Text = s.GetString("login_nick");
            _anonymous.Checked = s.GetBoolean("login_anonymous");
        }

        private void AnonymousOnCheckedChange(object sender, CompoundButton.CheckedChangeEventArgs checkedChangeEventArgs)
        {
            if (_anonymous.Checked)
            {
                _username.Visibility = ViewStates.Gone;
                _password.Visibility = ViewStates.Invisible;
                _nickName.Visibility = ViewStates.Visible;
                _nickName.RequestFocus();
            }
            else
            {
                _username.Visibility = ViewStates.Visible;
                _password.Visibility = ViewStates.Visible;
                _nickName.Visibility = ViewStates.Gone;
            }
        }

        private async void LoginButtonOnClick(object sender, EventArgs eventArgs)
        {
            var usr = _anonymous.Checked ? _nickName.Text : _username.Text;
            if (string.IsNullOrEmpty(usr)) return;
            
            _progressView.Visibility = ViewStates.Visible;

            var result = await Client.Login(usr, _password.Text, _anonymous.Checked);

            _progressView.Visibility = ViewStates.Gone;
            if (result?.Success ?? false)
            {
                StartActivity(new Intent(Application.Context,typeof(OfficesActivity)));
                Finish();
            }
            else
            {
                new Android.Support.V7.App.AlertDialog.Builder(this)
                    .SetPositiveButton("OKAY", (o, args) => {})
                    .SetMessage("Login Failed")
                    .Show();
                _username.SelectAll();
                _username.RequestFocus();
            }
        }
    }
}