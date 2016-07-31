﻿using Dopamine.Common.Services.Dialog;
using Dopamine.Common.Services.File;
using Dopamine.Common.Services.I18n;
using Dopamine.Common.Services.JumpList;
using Dopamine.Common.Services.Playback;
using Dopamine.Common.Services.Taskbar;
using Dopamine.Core.Base;
using Dopamine.Core.IO;
using Dopamine.Core.Logging;
using Dopamine.Core.Prism;
using Dopamine.Core.Settings;
using Dopamine.Core.Utils;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Windows;
using System.Windows.Media;

namespace Dopamine.ViewModels
{
    public class ShellViewModel : BindableBase
    {
        #region Variables
        private readonly IRegionManager regionManager;
        private IDialogService dialogService;
        private IPlaybackService playbackService;
        private II18nService i18nService;
        private ITaskbarService taskbarService;
        private IJumpListService jumpListService;
        private IFileService fileService;
        private bool isOverlayVisible;
        private string playPauseText;
        private ImageSource playPauseIcon;
        #endregion

        #region Commands
        public DelegateCommand<string> OpenLinkCommand { get; set; }
        public DelegateCommand<string> OpenMailCommand { get; set; }
        public DelegateCommand<string> OpenPathCommand { get; set; }
        public DelegateCommand PreviousCommand { get; set; }
        public DelegateCommand NextCommand { get; set; }
        public DelegateCommand LoadedCommand { get; set; }
        #endregion

        #region Properties
        public bool IsOverlayVisible
        {
            get { return this.isOverlayVisible; }
            set { SetProperty<bool>(ref this.isOverlayVisible, value); }
        }

        public string PlayPauseText
        {
            get { return this.playPauseText; }
            set { SetProperty<string>(ref this.playPauseText, value); }
        }

        public ImageSource PlayPauseIcon
        {
            get { return this.playPauseIcon; }
            set { SetProperty<ImageSource>(ref this.playPauseIcon, value); }
        }

        public ITaskbarService TaskbarService
        {
            get { return this.taskbarService; }
            set { this.taskbarService = value; }
        }
        #endregion

        #region Construction
        public ShellViewModel(IRegionManager regionManager, IDialogService dialogService, IPlaybackService playbackService, II18nService i18nService, ITaskbarService taskbarService, IJumpListService jumpListService, IFileService fileService)
        {
            this.regionManager = regionManager;
            this.dialogService = dialogService;
            this.playbackService = playbackService;
            this.i18nService = i18nService;
            this.taskbarService = taskbarService;
            this.jumpListService = jumpListService;
            this.fileService = fileService;

            // When starting, we're not playing yet
            this.ShowTaskBarItemInfoPause(false);

            this.TaskbarService.Description = ProductInformation.ApplicationDisplayName;

            // Event handlers
            this.dialogService.DialogVisibleChanged += iIsDialogVisible => { this.IsOverlayVisible = iIsDialogVisible; };

            this.OpenPathCommand = new DelegateCommand<string>((string ipath) =>
            {
                try
                {
                    Actions.TryOpenPath(ipath);
                }
                catch (Exception ex)
                {
                    LogClient.Instance.Logger.Error("Could not open the path {0} in Explorer. Exception: {1}", ipath, ex.Message);
                }
            });
            ApplicationCommands.OpenPathCommand.RegisterCommand(this.OpenPathCommand);

            this.OpenLinkCommand = new DelegateCommand<string>((string iLink) =>
            {
                try
                {
                    Actions.TryOpenLink(iLink);
                }
                catch (Exception ex)
                {
                    LogClient.Instance.Logger.Error("Could not open the link {0} in Internet Explorer. Exception: {1}", iLink, ex.Message);
                }
            });
            ApplicationCommands.OpenLinkCommand.RegisterCommand(this.OpenLinkCommand);

            this.OpenMailCommand = new DelegateCommand<string>((string emailAddress) =>
            {
                try
                {
                    Actions.TryOpenMail(emailAddress);
                }
                catch (Exception ex)
                {
                    LogClient.Instance.Logger.Error("Could not execute mailto command for the e-mail {0}. Exception: {1}", emailAddress, ex.Message);
                }
            });
            ApplicationCommands.OpenMailCommand.RegisterCommand(this.OpenMailCommand);

            this.PreviousCommand = new DelegateCommand(async () => await this.playbackService.PlayPreviousAsync());
            this.NextCommand = new DelegateCommand(async() => await this.playbackService.PlayNextAsync());

            this.LoadedCommand = new DelegateCommand(() => this.fileService.ProcessArguments(Environment.GetCommandLineArgs()));

            this.playbackService.PlaybackFailed += (iSender, iPlaybackFailedEventArgs) =>
            {
                this.TaskbarService.Description = ProductInformation.ApplicationDisplayName;
                this.TaskbarService.SetTaskbarProgressState(XmlSettingsClient.Instance.Get<bool>("Playback", "ShowProgressInTaskbar"), this.playbackService.IsPlaying);
                this.ShowTaskBarItemInfoPause(false);

                switch (iPlaybackFailedEventArgs.FailureReason)
                {
                    case PlaybackFailureReason.FileNotFound:
                        this.dialogService.ShowNotification(0xe711, 16, ResourceUtils.GetStringResource("Language_Error"), ResourceUtils.GetStringResource("Language_Error_Cannot_Play_This_Song_File_Not_Found"), ResourceUtils.GetStringResource("Language_Ok"), false, string.Empty);
                        break;
                    default:
                        this.dialogService.ShowNotification(0xe711, 16, ResourceUtils.GetStringResource("Language_Error"), ResourceUtils.GetStringResource("Language_Error_Cannot_Play_This_Song"), ResourceUtils.GetStringResource("Language_Ok"), true, ResourceUtils.GetStringResource("Language_Log_File"));
                        break;
                }
            };

            this.playbackService.PlaybackPaused += (_,__) =>
            {
                this.TaskbarService.SetTaskbarProgressState(XmlSettingsClient.Instance.Get<bool>("Playback", "ShowProgressInTaskbar"), this.playbackService.IsPlaying);
                this.ShowTaskBarItemInfoPause(false);
            };

            this.playbackService.PlaybackResumed += (_,__) =>
            {
                this.TaskbarService.SetTaskbarProgressState(XmlSettingsClient.Instance.Get<bool>("Playback", "ShowProgressInTaskbar"), this.playbackService.IsPlaying);
                this.ShowTaskBarItemInfoPause(true);
            };

            this.playbackService.PlaybackStopped += (_,__) =>
            {
                this.TaskbarService.Description = ProductInformation.ApplicationDisplayName;
                this.TaskbarService.SetTaskbarProgressState(false, false);
                this.ShowTaskBarItemInfoPause(false);
            };

            this.playbackService.PlaybackSuccess += (_) =>
            {
                this.TaskbarService.Description = this.playbackService.PlayingTrack.Artist.ArtistName + " - " + this.playbackService.PlayingTrack.Track.TrackTitle;
                this.TaskbarService.SetTaskbarProgressState(XmlSettingsClient.Instance.Get<bool>("Playback", "ShowProgressInTaskbar"), this.playbackService.IsPlaying);
                this.ShowTaskBarItemInfoPause(true);
            };

            this.playbackService.PlaybackProgressChanged += (_,__) => { this.TaskbarService.ProgressValue = this.playbackService.Progress; };

            // Populate the JumpList
            this.jumpListService.PopulateJumpListAsync();
        }
        #endregion

        #region Private

        private void ShowTaskBarItemInfoPause(bool showPause)
        {
            string value = "Play";

            try
            {
                if (showPause)
                {
                    value = "Pause";
                }

                this.PlayPauseText = Application.Current.TryFindResource("Language_" + value).ToString();

                Application.Current.Dispatcher.Invoke(() => { this.PlayPauseIcon = (ImageSource)new ImageSourceConverter().ConvertFromString("pack://application:,,,/Icons/TaskbarItemInfo_" + value + ".ico"); });
            }
            catch (Exception ex)
            {
                LogClient.Instance.Logger.Error("Could not change the TaskBarItemInfo Play/Pause icon to '{0}'. Exception: {1}", ex.Message, value);
            }

        }
        #endregion
    }

}