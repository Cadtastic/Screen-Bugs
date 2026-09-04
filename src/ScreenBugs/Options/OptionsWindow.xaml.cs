using System.Windows;
using System.Windows.Controls;
using ScreenBugs.Settings;

namespace ScreenBugs.Options;

/// <summary>
/// Live-preview options dialog. Every edit is applied to the overlay at once; Cancel restores the
/// snapshot taken when the window opened.
/// </summary>
public partial class OptionsWindow : Window
{
    private static readonly int[] FrameRates = [30, 60, 120];

    private readonly BugOptions initial;
    private readonly OptionsApplier applier;
    private readonly bool startupWasEnabled;
    private BugOptions edited;
    private bool previewRespawned;
    private bool suppress;

    // Explicit constructor: InitializeComponent plus control population.
    public OptionsWindow(BugOptions initial, OptionsApplier applier)
    {
        InitializeComponent();

        this.initial = initial;
        this.applier = applier;
        edited = initial;
        startupWasEnabled = StartupRegistration.IsEnabled();

        suppress = true;
        for (int count = 1; count <= BugTypeSlots.MaxSlots; count++)
        {
            SlotCountBox.Items.Add(count);
        }

        foreach (int rate in FrameRates)
        {
            FrameRateBox.Items.Add($"{rate} fps");
        }

        TypeChangeBox.Items.Add("Respawn all bugs");
        TypeChangeBox.Items.Add("Let existing bugs age out");

        SlotCountBox.SelectedItem = edited.TypeSlots.Count;
        FrameRateBox.SelectedIndex = Math.Max(0, Array.IndexOf(FrameRates, edited.FrameRate));
        TypeChangeBox.SelectedIndex = edited.OnTypeChange == TypeChangeBehavior.RespawnAll ? 0 : 1;
        CountSlider.Value = edited.BugCount;
        CountText.Text = edited.BugCount.ToString();
        StartupBox.IsChecked = startupWasEnabled;
        RebuildSlotRows();
        suppress = false;
    }

    /// <summary>The accepted options, or null if the dialog was cancelled or closed.</summary>
    public BugOptions? Result { get; private set; }

    /// <summary>Reverts a cancelled preview. Runs for OK too, but <see cref="Result"/> is set by then.</summary>
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (Result is not null)
        {
            return;
        }

        // Respawn on revert only if the preview actually replaced the population, or if the
        // original setting would have; otherwise cancelling would churn the screen for nothing.
        var revert = previewRespawned || initial.OnTypeChange == TypeChangeBehavior.RespawnAll
            ? TypeChangeBehavior.RespawnAll
            : TypeChangeBehavior.AgeOut;
        applier.Apply(edited, initial, revert);
    }

    private void RebuildSlotRows()
    {
        SlotPanel.Children.Clear();
        for (int index = 0; index < edited.TypeSlots.Count; index++)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            row.Children.Add(new TextBlock
            {
                Text = $"Type {index + 1}",
                Width = 130,
                VerticalAlignment = VerticalAlignment.Center,
            });

            var box = new ComboBox { Width = 200, DisplayMemberPath = nameof(BugTypeChoice.Label), Tag = index };
            foreach (var choice in BugTypeSlots.AvailableFor(edited.TypeSlots, index))
            {
                box.Items.Add(BugTypeChoice.From(choice));
            }

            box.SelectedItem = box.Items.Cast<BugTypeChoice>().First(item => item.Slot == edited.TypeSlots[index].Type);
            box.SelectionChanged += OnSlotChanged;
            row.Children.Add(box);

            var readout = new TextBlock
            {
                Width = 38,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Text = SpeedLabel(edited.TypeSlots[index].SpeedMultiplier),
            };

            var speed = new Slider
            {
                Width = 130,
                Minimum = SlotSetting.MinSpeed,
                Maximum = SlotSetting.MaxSpeed,
                TickFrequency = 0.25,
                IsSnapToTickEnabled = true,
                VerticalAlignment = VerticalAlignment.Center,
                Value = edited.TypeSlots[index].SpeedMultiplier,
                Tag = (index, readout),
            };
            speed.ValueChanged += OnSpeedChanged;
            row.Children.Add(speed);
            row.Children.Add(readout);

            SlotPanel.Children.Add(row);
        }
    }

    private void OnSlotChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppress || sender is not ComboBox { Tag: int index, SelectedItem: BugTypeChoice choice })
        {
            return;
        }

        var slots = edited.TypeSlots.ToList();
        slots[index] = slots[index] with { Type = choice.Slot };
        UpdateEdited(edited with { TypeSlots = slots });
        RebuildRowsSuppressed();
    }

    private void OnSpeedChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is not Slider { Tag: ValueTuple<int, TextBlock> tag })
        {
            return;
        }

        var (index, readout) = tag;
        float speed = SlotSetting.ClampSpeed((float)e.NewValue);
        readout.Text = SpeedLabel(speed);

        if (suppress || index >= edited.TypeSlots.Count)
        {
            return;
        }

        var slots = edited.TypeSlots.ToList();
        slots[index] = slots[index] with { SpeedMultiplier = speed };

        // Speeds are read live by the simulation, so this never respawns the population.
        UpdateEdited(edited with { TypeSlots = slots });
    }

    private static string SpeedLabel(float speed) => $"{speed:0.##}x";

    private void OnSlotCountChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppress || SlotCountBox.SelectedItem is not int count)
        {
            return;
        }

        UpdateEdited(edited with { TypeSlots = BugTypeSlots.Resize(edited.TypeSlots, count) });
        RebuildRowsSuppressed();
    }

    private void OnCountChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Slider coercion can fire this before the XAML fields are assigned.
        if (CountText is null)
        {
            return;
        }

        int count = (int)Math.Round(e.NewValue);
        CountText.Text = count.ToString();
        if (!suppress)
        {
            UpdateEdited(edited with { BugCount = count });
        }
    }

    private void OnFrameRateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppress || FrameRateBox.SelectedIndex < 0)
        {
            return;
        }

        UpdateEdited(edited with { FrameRate = FrameRates[FrameRateBox.SelectedIndex] });
    }

    private void OnTypeChangeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppress || TypeChangeBox.SelectedIndex < 0)
        {
            return;
        }

        var behavior = TypeChangeBox.SelectedIndex == 0 ? TypeChangeBehavior.RespawnAll : TypeChangeBehavior.AgeOut;
        UpdateEdited(edited with { OnTypeChange = behavior });
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        bool wantStartup = StartupBox.IsChecked == true;
        if (wantStartup)
        {
            // Always rewrite: cheap, and it repairs an entry left by an older install path.
            StartupRegistration.SetEnabled(true);
        }
        else if (startupWasEnabled)
        {
            StartupRegistration.SetEnabled(false);
        }

        Result = edited;
        Close();
    }

    private void UpdateEdited(BugOptions next)
    {
        var previous = edited;
        edited = next;
        if (applier.Apply(previous, next, next.OnTypeChange))
        {
            previewRespawned = true;
        }
    }

    private void RebuildRowsSuppressed()
    {
        suppress = true;
        RebuildSlotRows();
        suppress = false;
    }
}
