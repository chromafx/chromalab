/*
 * Copyright 2026 Ho Tzin Mein
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Avalonia.Threading;

namespace ChromaLab;

public partial class MainWindow
{
    private DispatcherTimer? _animationTimer;
    private int _animationFrame;
    private int _animationLoop;
    private bool _animationPlaying;

    private void WireAnimationUi()
    {
        PlayPauseButton.Click += (_, _) => ToggleAnimationPlayback();
    }

    private void ToggleAnimationPlayback()
    {
        if (!_session.HasAnimation || _busy)
            return;

        if (_animationPlaying)
            StopAnimationPlayback(restoreDocument: true);
        else
            StartAnimationPlayback();
    }

    private void StartAnimationPlayback()
    {
        if (!_session.HasAnimation || _busy)
            return;

        CancelPreviewWork();
        _session.ClearPreview();
        EndCompare();
        if (_canvasTool == CanvasTool.Crop)
            SetCanvasTool(CanvasTool.Hand);

        _animationPlaying = true;
        _animationFrame = 0;
        _animationLoop = 0;
        PlayPauseButton.Content = "Pause";
        ShowAnimationFrameAndSchedule();
    }

    private void StopAnimationPlayback(bool restoreDocument)
    {
        _animationTimer?.Stop();
        _animationTimer = null;
        var wasPlaying = _animationPlaying;
        _animationPlaying = false;
        PlayPauseButton.Content = "Play";

        if (restoreDocument && (wasPlaying || _session.IsShowingAnimationFrame))
        {
            _session.RestoreDocumentPreview();
            if (!_busy && _session.HasImage)
                StatusText.Text = "Ready";
        }

        UpdateAnimationUi();
    }

    private void ShowAnimationFrameAndSchedule()
    {
        if (!_animationPlaying || !_session.HasAnimation)
            return;

        var count = _session.AnimationFrameCount;
        _session.ShowAnimationFrame(_animationFrame);
        StatusText.Text = $"Playing · frame {_animationFrame + 1}/{count}";
        UpdateAnimationUi();

        var delayMs = _session.GetAnimationFrameDelayMs(_animationFrame);
        _animationTimer ??= new DispatcherTimer();
        _animationTimer.Stop();
        _animationTimer.Interval = TimeSpan.FromMilliseconds(delayMs);
        _animationTimer.Tick -= OnAnimationTick;
        _animationTimer.Tick += OnAnimationTick;
        _animationTimer.Start();
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        if (!_animationPlaying || !_session.HasAnimation)
        {
            StopAnimationPlayback(restoreDocument: true);
            return;
        }

        var count = _session.AnimationFrameCount;
        _animationFrame++;
        if (_animationFrame >= count)
        {
            _animationFrame = 0;
            var repeats = _session.AnimationRepeatCount;
            if (repeats != 0)
            {
                _animationLoop++;
                if (_animationLoop >= repeats)
                {
                    StopAnimationPlayback(restoreDocument: true);
                    return;
                }
            }
        }

        ShowAnimationFrameAndSchedule();
    }

    private void UpdateAnimationUi()
    {
        var hasAnim = _session.HasAnimation;
        AnimationPane.IsVisible = hasAnim && _railMode == LeftRailMode.Tools;
        PlayPauseButton.IsEnabled = hasAnim && !_busy;
        if (!hasAnim)
        {
            AnimationFrameText.Text = "";
            return;
        }

        var count = _session.AnimationFrameCount;
        AnimationFrameText.Text = _animationPlaying
            ? $"Frame {_animationFrame + 1}/{count}"
            : $"{count} frames · play-only";
    }
}
