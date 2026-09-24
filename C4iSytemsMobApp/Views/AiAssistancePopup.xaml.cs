using C4iSytemsMobApp.Models;
using C4iSytemsMobApp.Services;
using CommunityToolkit.Maui.Views;

namespace C4iSytemsMobApp.Views;

/// <summary>
/// AI Assistance popup for the Incident Report description. Closes with the text the guard chose
/// to use, or null when they cancel - the page is the only thing that writes the description.
/// Port of ai-assistance.js on /Incident/Register.
/// </summary>
public partial class AiAssistancePopup : Popup
{
    private readonly AiAssistanceService _ai;
    private readonly string _original;       // the guard's text, exactly as typed
    private string _grammarBase = string.Empty;   // the text the server checked; match offsets point into it
    private List<AiGrammarMatch> _matches = new();
    private string _result = string.Empty;   // rewrite / translation awaiting review
    private bool _busy;
    private bool _closed;

    public AiAssistancePopup(AiAssistanceService ai, string originalText)
    {
        InitializeComponent();

        _ai = ai;
        _original = originalText ?? string.Empty;

        // Fit the phone: nearly full width, most of the height, capped for tablets.
        var display = DeviceDisplay.Current.MainDisplayInfo;
        var density = display.Density > 0 ? display.Density : 1;
        var width = Math.Min(display.Width / density - 24, 520);
        var height = Math.Min(display.Height / density * 0.85, 760);
        Size = new Size(width, height);

        ShowStep(ChoicesStep);
    }

    // ---------------------------------------------------------------------------------------
    // Steps, busy state and messages
    // ---------------------------------------------------------------------------------------

    /* One place decides which panel and which footer buttons are on screen, so the footer can
       never disagree with the step being shown. */
    private void ShowStep(View step)
    {
        ChoicesStep.IsVisible = step == ChoicesStep;
        LanguageStep.IsVisible = step == LanguageStep;
        GrammarStep.IsVisible = step == GrammarStep;
        ResultStep.IsVisible = step == ResultStep;

        BackButton.IsVisible = step != ChoicesStep;
        UseGrammarButton.IsVisible = step == GrammarStep;
        ImproveButton.IsVisible = step == GrammarStep;
        TranslateButton.IsVisible = step == LanguageStep;
        UseResultButton.IsVisible = step == ResultStep;
    }

    /* Locks the popup while a request is in flight, so a second tap cannot start another one.
       Cancel stays enabled so the guard is never stuck waiting. */
    private void SetBusy(bool isBusy, string message = null)
    {
        _busy = isBusy;
        BusyLabel.Text = message ?? "Processing with AI...";
        BusyPanel.IsVisible = isBusy;

        foreach (var button in new[] { ChoiceGrammarButton, ChoiceTranslateButton, BackButton,
                                       UseGrammarButton, ImproveButton, TranslateButton, UseResultButton })
            button.IsEnabled = !isBusy;

        TargetLanguagePicker.IsEnabled = !isBusy;

        // A grammar run with nothing to apply keeps its Use button disabled after the request.
        if (!isBusy)
            UseGrammarButton.IsEnabled = _matches.Any(z => z.Replacements.Count > 0);
    }

    private void ShowAlert(string message)
    {
        AlertLabel.Text = message;
        AlertPanel.IsVisible = true;
    }

    private void ClearAlert()
    {
        AlertLabel.Text = string.Empty;
        AlertPanel.IsVisible = false;
    }

    /// <summary>
    /// Runs one provider call with the busy state and error handling every option shares. Returns
    /// default when it failed or when the guard closed the popup while it ran.
    /// </summary>
    private async Task<T> RunAsync<T>(Func<Task<T>> operation, string busyMessage, string failureMessage)
    {
        ClearAlert();
        SetBusy(true, busyMessage);

        try
        {
            var result = await operation();
            return _closed ? default : result;
        }
        catch (AiAssistanceUnavailableException ex)
        {
            if (!_closed) ShowAlert(ex.Message);
        }
        catch (Exception)
        {
            if (!_closed) ShowAlert(failureMessage);
        }
        finally
        {
            if (!_closed) SetBusy(false);
        }

        return default;
    }

    // ---------------------------------------------------------------------------------------
    // Grammar & Scenario Check
    // ---------------------------------------------------------------------------------------

    private async void OnChoiceGrammarClicked(object sender, EventArgs e)
    {
        if (_busy) return;

        var result = await RunAsync(() => _ai.CheckGrammarAsync(_original),
            "Checking grammar...", AiAssistanceService.GrammarUnavailableMessage);

        if (result == null) return;

        RenderGrammar(result);
        ShowStep(GrammarStep);
    }

    private void RenderGrammar(AiGrammarCheckResult result)
    {
        GrammarLanguageLabel.Text = string.IsNullOrWhiteSpace(result.DetectedLanguage) ? "Unknown" : result.DetectedLanguage;
        // The server checks the trimmed text, so its offsets are applied to that same string.
        _grammarBase = result.OriginalText ?? _original;
        GrammarOriginalLabel.Text = _grammarBase;

        // Only the findings that can actually change something are selectable.
        _matches = result.Matches.Where(z => z.Replacements.Count > 0).ToList();
        foreach (var match in _matches)
            match.Applied = true;   // suggestions start accepted; the guard can untick any of them

        var unusable = result.Matches.Count - _matches.Count;

        GrammarSuggestionsList.Children.Clear();

        if (_matches.Count == 0)
        {
            GrammarSuggestionsList.Children.Add(new Label
            {
                Text = "No spelling or grammar corrections were suggested.",
                FontSize = 12,
                TextColor = Color.FromArgb("#28A745")
            });
        }
        else
        {
            foreach (var match in _matches)
                GrammarSuggestionsList.Children.Add(BuildSuggestionRow(match));
        }

        if (unusable > 0)
        {
            GrammarSuggestionsList.Children.Add(new Label
            {
                Text = $"{unusable} further issue(s) were flagged with no suggested correction.",
                FontSize = 12,
                TextColor = Colors.Gray
            });
        }

        UseGrammarButton.IsEnabled = _matches.Count > 0;
        RenderCorrected();
    }

    /* Ticking is per suggestion, so the guard decides each correction rather than taking all or
       none. An ignored suggestion keeps its row - the guard can change their mind - but drops the
       tick and is dimmed and struck through. */
    private View BuildSuggestionRow(AiGrammarMatch match)
    {
        var tick = new Label
        {
            Text = "✓",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#28A745"),
            VerticalOptions = LayoutOptions.Start,
            WidthRequest = 18
        };

        var replacement = new Span
        {
            Text = match.Replacements[0],
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#212529")
        };

        var change = new Label
        {
            FontSize = 12,
            LineBreakMode = LineBreakMode.WordWrap,
            FormattedText = new FormattedString
            {
                Spans =
                {
                    new Span { Text = $"\"{match.Original}\" → ", TextColor = Color.FromArgb("#212529") },
                    replacement
                }
            }
        };

        var message = new Label
        {
            Text = match.Message ?? string.Empty,
            FontSize = 11,
            TextColor = Color.FromArgb("#6C757D"),
            LineBreakMode = LineBreakMode.WordWrap,
            IsVisible = !string.IsNullOrWhiteSpace(match.Message)
        };

        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 6
        };
        grid.Add(tick, 0, 0);
        grid.Add(new VerticalStackLayout { Spacing = 2, Children = { change, message } }, 1, 0);

        var row = new Border
        {
            Stroke = Color.FromArgb("#DEE2E6"),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
            BackgroundColor = Colors.White,
            Padding = new Thickness(6),
            Content = grid
        };

        void Refresh()
        {
            tick.Opacity = match.Applied ? 1 : 0;
            row.Opacity = match.Applied ? 1 : 0.55;
            replacement.TextDecorations = match.Applied ? TextDecorations.None : TextDecorations.Strikethrough;
        }

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            if (_busy) return;
            match.Applied = !match.Applied;
            Refresh();
            RenderCorrected();
        };
        row.GestureRecognizers.Add(tap);

        Refresh();
        return row;
    }

    private void RenderCorrected() =>
        GrammarCorrectedLabel.Text = AiAssistanceService.BuildCorrectedText(_grammarBase, _matches);

    // Applies the ticked corrections straight to the description - no second review screen.
    private void OnUseGrammarClicked(object sender, EventArgs e)
    {
        if (_busy) return;
        CloseWith(AiAssistanceService.BuildCorrectedText(_grammarBase, _matches));
    }

    private async void OnImproveClicked(object sender, EventArgs e)
    {
        if (_busy) return;

        var result = await RunAsync(() => _ai.ImproveIncidentReportAsync(_original),
            "Processing with AI...", AiAssistanceService.UnavailableMessage);

        if (string.IsNullOrWhiteSpace(result)) return;

        ShowResult(result);
    }

    // ---------------------------------------------------------------------------------------
    // Language Converter
    // ---------------------------------------------------------------------------------------

    private async void OnChoiceTranslateClicked(object sender, EventArgs e)
    {
        if (_busy) return;

        ClearAlert();
        DetectedLanguageLabel.Text = "detecting...";
        ShowStep(LanguageStep);

        SetBusy(true, "Detecting language...");

        // As on the web: the language list and the detection are requested together.
        var languagesTask = LoadLanguagesAsync();
        var detectTask = _ai.CheckGrammarAsync(_original);

        await languagesTask;

        // Reuses the grammar check purely for its language detection. A failure here is not an
        // error for the guard - conversion does not need it - so it just reads "Unknown".
        try
        {
            var result = await detectTask;
            if (!_closed)
                DetectedLanguageLabel.Text = string.IsNullOrWhiteSpace(result?.DetectedLanguage) ? "Unknown" : result.DetectedLanguage;
        }
        catch (Exception)
        {
            if (!_closed) DetectedLanguageLabel.Text = "Unknown";
        }
        finally
        {
            if (!_closed) SetBusy(false);
        }
    }

    /* The list comes from the server so it can be changed in configuration, not in the app.
       Loaded once per popup; a failure leaves the picker empty and says so. */
    private async Task LoadLanguagesAsync()
    {
        if (TargetLanguagePicker.ItemsSource is { Count: > 0 }) return;

        try
        {
            var languages = await _ai.GetLanguagesAsync();
            if (_closed) return;

            TargetLanguagePicker.ItemsSource = languages;
            if (languages.Count > 0)
                TargetLanguagePicker.SelectedIndex = 0;
            else
                ShowAlert("The language list could not be loaded. Please try again.");
        }
        catch (AiAssistanceUnavailableException ex)
        {
            if (!_closed) ShowAlert(ex.Message);
        }
        catch (Exception)
        {
            if (!_closed) ShowAlert("The language list could not be loaded. Please try again.");
        }
    }

    private async void OnTranslateClicked(object sender, EventArgs e)
    {
        if (_busy) return;

        if (TargetLanguagePicker.SelectedItem is not AiLanguageOption target)
        {
            ShowAlert("The selected language is not supported.");
            return;
        }

        var result = await RunAsync(() => _ai.TranslateAsync(_original, target.Code),
            "Translating text...", AiAssistanceService.UnavailableMessage);

        if (string.IsNullOrWhiteSpace(result)) return;

        ShowResult(result);
    }

    // ---------------------------------------------------------------------------------------
    // Review - for the rewrite and the conversion, which replace the whole text
    // ---------------------------------------------------------------------------------------

    private void ShowResult(string result)
    {
        _result = result;
        ResultOriginalLabel.Text = _original;
        ResultTextLabel.Text = _result;
        ShowStep(ResultStep);
    }

    private void OnUseResultClicked(object sender, EventArgs e)
    {
        if (_busy || string.IsNullOrEmpty(_result)) return;
        CloseWith(_result);
    }

    private void OnBackClicked(object sender, EventArgs e)
    {
        if (_busy) return;
        ClearAlert();
        ShowStep(ChoicesStep);
    }

    private void OnCancelClicked(object sender, EventArgs e) => CloseWith(null);

    private void CloseWith(string text)
    {
        if (_closed) return;
        _closed = true;
        Close(text);
    }
}
