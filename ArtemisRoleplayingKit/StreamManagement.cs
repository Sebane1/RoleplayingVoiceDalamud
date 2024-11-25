using Dalamud.Game.Config;
using Dalamud.Plugin;
using RoleplayingMediaCore.Twitch;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoleplayingVoice {
    public partial class Plugin : IDalamudPlugin {
        #region Stream Management
        private void TuneIntoStream(string url, RoleplayingMediaCore.IMediaGameObject audioGameObject, bool isNotTwitch) {
            Task.Run(async () => {
                string cleanedURL = RemoveSpecialSymbols(url);
                _streamURLs = isNotTwitch ? new string[] { url } : TwitchFeedManager.GetServerResponse(cleanedURL);
                _videoWindow.IsOpen = config.DefaultTwitchOpen == 0;
                if (_streamURLs.Length > 0) {
                    _mediaManager.PlayStream(audioGameObject, _streamURLs[(int)_videoWindow.FeedType]);
                    lastStreamURL = cleanedURL;
                    if (!isNotTwitch) {
                        _currentStreamer = cleanedURL.Replace(@"https://", null).Replace(@"www.", null).Replace("twitch.tv/", null);
                        _chat?.Print(@"Tuning into " + _currentStreamer + @"! Wanna chat? Use ""/artemis twitch""." +
                            "\r\nYou can also use \"/artemis video\" to toggle the video feed!" +
                            (!IsResidential() ? "\r\nIf you need to end a stream in a public space you can leave the zone or use \"/artemis endlisten\"" : ""));
                    } else {
                        _currentStreamer = "RTMP Streamer";
                        _chat?.Print(@"Tuning into a custom RTMP stream!" +
                            "\r\nYou can also use \"/artemis video\" to toggle the video feed!" +
                            (!IsResidential() ? "\r\nIf you need to end a stream in a public space you can leave the zone or use \"/artemis endlisten\"" : ""));
                    }
                }
            });
            streamWasPlaying = true;
            try {
                _gameConfig.Set(SystemConfigOption.IsSndBgm, true);
            } catch (Exception e) {
                Plugin.PluginLog?.Warning(e, e.Message);
            }
            _streamSetCooldown.Stop();
            _streamSetCooldown.Reset();
            _streamSetCooldown.Start();
        }
        private void ChangeStreamQuality() {
            if (_streamURLs != null) {
                if (streamWasPlaying && _streamURLs.Length > 0) {
                    Task.Run(async () => {
                        if ((int)_videoWindow.FeedType < _streamURLs.Length) {
                            if (_lastStreamObject != null) {
                                try {
                                    _mediaManager.ChangeStream(_lastStreamObject, _streamURLs[(int)_videoWindow.FeedType], _videoWindow.Size.Value.X);
                                } catch (Exception e) {
                                    Plugin.PluginLog?.Warning(e, e.Message);
                                }
                            }
                        }
                    });
                }
            }
        }
        #endregion

    }
}
