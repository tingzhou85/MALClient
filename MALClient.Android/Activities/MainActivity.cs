﻿using System;
using System.Net;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Support.Design.Widget;
using Android.Support.V7.App;
using Android.Views;
using Android.Widget;
using GalaSoft.MvvmLight.Ioc;
using MALClient.Android.ViewModels;
using MALClient.XShared.Utils.Enums;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Interfaces;
using Fragment = Android.Support.V4.App.Fragment;

namespace MALClient.Android.Activities
{
    [Activity(Label = "MALClient", MainLauncher = true, 
        Icon = "@drawable/icon", 
        LaunchMode = LaunchMode.SingleTop,
        Theme = "@style/Theme.AppCompat")]
    public partial class MainActivity : AppCompatActivity , IDimensionsProvider
    {
        private MainViewModel _viewModel;
        private bool _addedNavHandlers;

        private MainViewModel ViewModel => _viewModel ?? (_viewModel = SimpleIoc.Default.GetInstance<MainViewModel>());

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
            SetContentView(Resource.Layout.MainPage);
            if (!_addedNavHandlers)
            {
                _addedNavHandlers = true;

                ViewModel.MainNavigationRequested += ViewModelOnMainNavigationRequested;
                NavView.NavigationItemSelected += NavViewOnNavigationItemSelected;

                ViewModel.Navigate(PageIndex.PageLogIn);

            }
    
        }

        private void ViewModelOnMainNavigationRequested(Fragment fragment)
        {
            SupportFragmentManager.BeginTransaction()
                .Replace(Resource.Id.MainContentFrame, fragment)
                .Commit();
        }

        private void NavViewOnNavigationItemSelected(object sender, NavigationView.NavigationItemSelectedEventArgs e)
        {
            e.MenuItem.SetChecked(true);

            switch (e.MenuItem.ItemId)
            {
                case Resource.Id.MainHamburgerBtnLogIn:
                    ViewModelLocator.GeneralMain.Navigate(PageIndex.PageLogIn);
                    break;
            }

            DrawerLayout.CloseDrawers();
        }

        public double ActualWidth => 800;
        public double ActualHeight => 1200;
    }
}
