﻿using System;
using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Support.V7.App;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using norsu.ass;
using norsu.ass.Network;

namespace norsu.ass
{
    [Activity(Theme = "@style/AppTheme.NoActionBar", ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation,
        ScreenOrientation = ScreenOrientation.Portrait)]
    public class OfficesActivity : Activity
    {
        private ListView _offices;
        private TextView _username,_fullname;
        private ImageView _picture;
        
        protected override async void OnCreate(Bundle savedInstanceState)
        {
            var dlg = new Android.App.AlertDialog.Builder(this);
            if (Client.Server == null)
            {
                dlg.SetTitle(Resource.String.no_server_title);
                dlg.SetMessage(Resource.String.no_server_message);
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
                    try
                    {
                        if (CurrentFocus != null)
                        {
                            var imm = (InputMethodManager) GetSystemService(Context.InputMethodService);
                            imm.HideSoftInputFromWindow(CurrentFocus.WindowToken, 0);
                        }
                        dlg = new Android.App.AlertDialog.Builder(this);
                        dlg.SetTitle(Resource.String.server_shutdown_title);
                        dlg.SetMessage(Resource.String.server_shutdown_message);
                        dlg.SetPositiveButton("Exit", (sender, args) =>
                        {
                            FinishAffinity();
                        });
                        dlg.SetCancelable(false);
                        dlg.Show();

                    }
                    catch (Exception e)
                    {
                        FinishAffinity();
                    }
                });
            });

            base.OnCreate(savedInstanceState);

            if (string.IsNullOrEmpty(Client.Username))
            {
                StartActivity(new Intent(Application.Context,typeof(LoginActivity)));
                Finish();
            }
            
            SetContentView(Resource.Layout.Offices);

            _username = FindViewById<TextView>(Resource.Id.userName);
            _fullname = FindViewById<TextView>(Resource.Id.name);
            _picture = FindViewById<ImageView>(Resource.Id.picture);
            
            _offices = FindViewById<ListView>(Resource.Id.officesList);
            _offices.ItemClick += OfficesOnItemClick;
            
            _username.Text = Client.Username;
            _fullname.Text = Client.Fullname;
            var usr = Client.GetPicture(Client.UserId);
            if (usr?.Picture.Length>0)
            {
                try
                {
                    var pic = BitmapFactory.DecodeByteArray(usr.Picture, 0, usr.Picture.Length);
                    _picture.SetImageBitmap(pic);
                }
                catch (Exception e)
                {
                    //
                }
            }
            else
            {
                Messenger.Default.AddListener<UserPicture>(Messages.PictureReceived,
                    user =>
                    {
                        if (user.UserId != Client.UserId) return;
                        RunOnUiThread(() =>
                        {
                            try
                            {
                                _picture.SetImageBitmap(BitmapFactory.DecodeByteArray(user.Picture, 0,
                                    user.Picture.Length));
                            }
                            catch (Exception e)
                            {
                                //
                            }
                            
                        });
                    });
            }

            var offices = await Client.GetOffices();
            
            while (offices == null)
            {
                
                offices = await Client.GetOffices();
            }
            var adapter = new OfficesAdapter(this, offices.Items);

            _offices.Adapter = adapter;

            var progress = FindViewById<ProgressBar>(Resource.Id.progress);
            progress.Visibility = ViewStates.Gone;
            
        }

        private void OfficesOnItemClick(object o, AdapterView.ItemClickEventArgs e)
        {
            var office = ((OfficesAdapter) _offices.Adapter)[e.Position];

            Client.SelectedOffice = office;
            StartActivity(typeof(OfficeActivity));
        }
        
    }
}