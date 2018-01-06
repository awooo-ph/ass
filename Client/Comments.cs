﻿using System;
using System.Collections.Generic;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using norsu.ass.Network;

namespace norsu.ass
{
    [Activity(Label = "RatingsActivity",ParentActivity = typeof(RatingsActivity),
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation,
        ScreenOrientation = ScreenOrientation.Portrait)]
    public class CommentsActivity : ListActivity
    {
        private long SuggestionId = 0;
        private EditText _comment;
        private Button _send;
        private CommentsAdapter _adapter;
        private Comments _comments;
        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            
            Title = Intent.GetStringExtra("name");
            SuggestionId = Intent.GetLongExtra("id",0);
            
            SetContentView(Resource.Layout.Comments);

            _comment = FindViewById<EditText>(Resource.Id.comment);
            _send = FindViewById<Button>(Resource.Id.send);

            FindViewById<TextView>(Resource.Id.title).Text = Intent.GetStringExtra("title");
            FindViewById<TextView>(Resource.Id.body).Text = Intent.GetStringExtra("body");

            _send.Click += SendOnClick;

            _comments = await Client.GetComments(SuggestionId);

            FindViewById<ProgressBar>(Resource.Id.progress).Visibility = ViewStates.Gone;
            
            if (_comments != null)
            {
                _adapter = new CommentsAdapter(this,_comments.Items);
                ListAdapter = _adapter;
            }
        }

        protected override void OnSaveInstanceState(Bundle outState)
        {
            outState.PutLong("suggestionId",SuggestionId);
            base.OnSaveInstanceState(outState);
        }

        protected override void OnRestoreInstanceState(Bundle savedInstanceState)
        {
            base.OnRestoreInstanceState(savedInstanceState);
            SuggestionId = savedInstanceState.GetLong("suggestionId");
        }

        private async void SendOnClick(object sender, EventArgs eventArgs)
        {
            _send.Enabled = false;
            _comment.Enabled = false;

            if (await Client.AddComment(SuggestionId, _comment.Text))
            {
                Toast.MakeText(this, "Comment sent", ToastLength.Short);
                _comments.Items.Add(new Comment()
                {
                    Message = _comment.Text,
                    Sender = Client.Username,
                });
                _adapter = new CommentsAdapter(this, _comments.Items);
                ListAdapter = _adapter;
            }
            _send.Enabled = true;
            _comment.Enabled = true;
            _comment.Text = "";
        }
    }
}