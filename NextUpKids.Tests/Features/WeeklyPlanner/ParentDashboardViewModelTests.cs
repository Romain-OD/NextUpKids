using AwesomeAssertions;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Storage;
using NextUpKids.Common.Database;
using NextUpKids.Common.Messages;
using NextUpKids.Common.Services;
using NextUpKids.Features.WeeklyPlanner;
using NSubstitute;

namespace NextUpKids.Tests.Features.WeeklyPlanner;

public class ParentDashboardViewModelTests
{
	static ParentDashboardViewModel Create(TestDatabase scope, IMessenger? messenger = null, IVoiceService? voice = null)
	{
		var files = Substitute.For<IFileSystem>();
		files.AppDataDirectory.Returns(scope.StoragePath);
		return new(scope.Database, voice ?? Substitute.For<IVoiceService>(), messenger ?? new WeakReferenceMessenger(), files);
	}

	[Fact]
	public async Task Steps_can_be_added_edited_reordered_deleted_and_reloaded()
	{
		await using var scope = new TestDatabase();
		var messenger = new WeakReferenceMessenger();
		var changes = new List<RoutineChangedMessage>();
		messenger.Register<RoutineChangedMessage>(changes, (r, m) => ((List<RoutineChangedMessage>)r).Add(m));
		var vm = Create(scope, messenger);
		await vm.LoadAsync();
		vm.NewStepCommand.Execute(null);
		vm.StepTitle = "Hockey";
		vm.StepImage = "hockey.png";
		await vm.SaveStepCommand.ExecuteAsync(null);
		vm.Steps.Select(s => s.Title).Should().Equal("S'habiller", "Prendre le petit déjeuner", "Se brosser les dents", "Hockey");
		vm.SelectedStep = vm.Steps[3];
		vm.StepTitle = "Patinage";
		vm.StepImage = "skating.png";
		await vm.SaveStepCommand.ExecuteAsync(null);
		vm.SelectedStep = vm.Steps[3];
		await vm.MoveUpCommand.ExecuteAsync(null);
		vm.Steps.Select(s => s.Title).Should().Equal("S'habiller", "Prendre le petit déjeuner", "Patinage", "Se brosser les dents");
		vm.SelectedStep = vm.Steps[2];
		await vm.DeleteStepCommand.ExecuteAsync(null);
		var reopened = Create(scope);
		await reopened.LoadAsync();
		reopened.Steps.Select(s => s.Title).Should().Equal("S'habiller", "Prendre le petit déjeuner", "Se brosser les dents");
		reopened.Steps.Select(s => s.Order).Should().Equal(0, 1, 2);
		changes.Should().HaveCount(4);
	}

	[Fact]
	public async Task Duplicate_copies_all_four_periods_without_sharing_row_identity()
	{
		await using var scope = new TestDatabase();
		var vm = Create(scope);
		await vm.LoadAsync();
		vm.StepTitle = "Natation";
		vm.StepImage = "swimming.png";
		await vm.SaveStepCommand.ExecuteAsync(null);
		vm.CopyTargets = [ParentDashboardViewModel.Days[1], ParentDashboardViewModel.Days[2], ParentDashboardViewModel.Days[0]];
		await vm.DuplicateDayCommand.ExecuteAsync(null);
		var db = await scope.Database.ConnectionAsync();
		var monday = await db.Table<RoutineStep>().Where(s => s.Day == DayOfWeek.Monday).ToListAsync();
		var tuesday = await db.Table<RoutineStep>().Where(s => s.Day == DayOfWeek.Tuesday).ToListAsync();
		tuesday.Select(s => s.Title).Should().BeEquivalentTo(monday.Select(s => s.Title));
		tuesday.Select(s => s.Id).Intersect(monday.Select(s => s.Id)).Should().BeEmpty();
		tuesday.Select(s => s.Period).Distinct().Should().HaveCount(4);
		await vm.SelectAsync(DayOfWeek.Tuesday, Period.BeforeSchool);
		vm.SelectedStep = vm.Steps.Last();
		await vm.DeleteStepCommand.ExecuteAsync(null);
		await vm.SelectAsync(DayOfWeek.Monday, Period.BeforeSchool);
		vm.Steps.Should().Contain(s => s.Title == "Natation");
		await vm.SelectAsync(DayOfWeek.Wednesday, Period.BeforeSchool);
		vm.Steps.Should().Contain(s => s.Title == "Natation");
	}

	[Fact]
	public async Task Settings_are_global_and_preview_uses_the_selected_language_and_voice()
	{
		await using var scope = new TestDatabase();
		var voice = Substitute.For<IVoiceService>();
		voice.SpeakAsync(Arg.Any<string>(), Arg.Any<AppLanguage>(), Arg.Any<VoiceMode>(), Arg.Any<CancellationToken>()).Returns(true);
		var vm = Create(scope, voice: voice);
		await vm.SelectAsync(DayOfWeek.Monday, Period.Bedtime);
		vm.StartTime = new TimeSpan(20, 15, 0);
		vm.LanguageIndex = 1;
		vm.VoiceIndex = 1;
		vm.StepTitle = "Good night";
		await vm.PreviewCommand.ExecuteAsync(null);
		await voice.Received().SpeakAsync("Good night", AppLanguage.English, VoiceMode.Soft, Arg.Any<CancellationToken>());
		await vm.SaveSettingsCommand.ExecuteAsync(null);
		await vm.SelectAsync(DayOfWeek.Sunday, Period.Bedtime);
		vm.StartTime.Should().Be(new TimeSpan(20, 15, 0));
		vm.LanguageIndex.Should().Be(1);
		vm.VoiceIndex.Should().Be(1);
	}

	[Fact]
	public async Task Invalid_title_image_and_trigger_settings_do_not_overwrite_saved_data()
	{
		await using var scope = new TestDatabase();
		var vm = Create(scope);
		await vm.LoadAsync();
		vm.SelectedStep = vm.Steps[0];
		vm.StepTitle = " ";
		await vm.SaveStepCommand.ExecuteAsync(null);
		vm.TitleError.Should().Be("Le titre doit contenir entre 1 et 140 caractères.");
		vm.StepTitle = "Nouveau titre";
		vm.TitleError.Should().BeEmpty();
		vm.StepImage = "..\\private.png";
		await vm.SaveStepCommand.ExecuteAsync(null);
		vm.Error.Should().Be("Choisissez une illustration ou importez une photo.");
		vm.StartTime = TimeSpan.FromHours(25);
		await vm.SaveSettingsCommand.ExecuteAsync(null);
		vm.Error.Should().Be("Choisissez une heure entre 00:00 et 23:59.");
		vm.StartTime = new TimeSpan(18, 0, 0);
		await vm.SaveSettingsCommand.ExecuteAsync(null);
		vm.Error.Should().Be("Chaque période doit avoir une heure différente.");
		await vm.LoadAsync();
		vm.Steps[0].Title.Should().Be("S'habiller");
		vm.StartTime.Should().Be(new TimeSpan(7, 0, 0));
	}

	[Fact]
	public async Task Photo_import_copies_privately_and_cancel_or_invalid_input_keeps_the_previous_image()
	{
		await using var scope = new TestDatabase();
		var vm = Create(scope);
		await vm.LoadAsync();
		byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aS2kAAAAASUVORK5CYII=");
		await using var stream = new MemoryStream(png);
		await vm.ImportPhotoAsync(stream, "..\\photo.png", TestContext.Current.CancellationToken);
		var imported = vm.StepImage;
		imported.Should().StartWith(scope.StoragePath);
		(await File.ReadAllBytesAsync(imported, TestContext.Current.CancellationToken)).Should().Equal(png);
		using var cancelled = new CancellationTokenSource();
		cancelled.Cancel();
		await vm.ImportPhotoAsync(new MemoryStream(png), "another.png", cancelled.Token);
		vm.StepImage.Should().Be(imported);
		await vm.ImportPhotoAsync(new MemoryStream(png), "file.txt", TestContext.Current.CancellationToken);
		vm.StepImage.Should().Be(imported);
		vm.Error.Should().Be("Format photo non pris en charge.");
		vm.StepTitle = "Ma photo";
		await vm.SaveStepCommand.ExecuteAsync(null);
		var reloaded = Create(scope);
		await reloaded.LoadAsync();
		reloaded.Steps.Last().Image.Should().Be(imported);
		vm.NewStepCommand.Execute(null);
		File.Exists(imported).Should().BeTrue();
	}

	[Theory]
	[InlineData(0, false)]
	[InlineData(20971520, true)]
	[InlineData(20971521, false)]
	public async Task Photo_size_limits_leave_no_partial_file(int length, bool accepted)
	{
		await using var scope = new TestDatabase();
		var vm = Create(scope);
		await using var input = new MemoryStream(new byte[length]);
		await vm.ImportPhotoAsync(input, "photo.png", TestContext.Current.CancellationToken);
		var files = Directory.GetFiles(Path.Combine(scope.StoragePath, "Photos"));
		if (accepted)
		{
			files.Should().ContainSingle();
			new FileInfo(vm.StepImage).Length.Should().Be(length);
		}
		else
		{
			files.Should().BeEmpty();
			vm.StepImage.Should().Be("play.png");
			vm.Error.Should().Be("La photo doit contenir une image de moins de 20 Mo.");
		}
	}

	[Theory]
	[InlineData(-1, 0)]
	[InlineData(2, 0)]
	[InlineData(0, -1)]
	[InlineData(0, 3)]
	public async Task Invalid_voice_choices_preserve_the_stored_settings(int language, int mode)
	{
		await using var scope = new TestDatabase();
		var vm = Create(scope);
		await vm.LoadAsync();
		vm.LanguageIndex = language;
		vm.VoiceIndex = mode;
		await vm.SaveSettingsCommand.ExecuteAsync(null);
		vm.Error.Should().Be("Choisissez une langue et un mode de voix valides.");
		await vm.LoadAsync();
		vm.LanguageIndex.Should().Be(0);
		vm.VoiceIndex.Should().Be(0);
	}

	[Fact]
	public async Task Successive_quick_adds_keep_existing_steps_and_confirm_each_change()
	{
		await using var scope = new TestDatabase();
		var vm = Create(scope);
		await vm.LoadAsync();
		vm.ChooseIconCommand.Execute(IconCatalog.All.First(i => i.Image == "shower.png"));
		vm.StepTitle.Should().Be("Douche");
		vm.ChooseIconCommand.Execute(IconCatalog.All.First(i => i.Image == "hockey.png"));
		vm.StepTitle.Should().Be("Hockey", "an auto-filled title follows the chosen icon");
		vm.HasUnsavedChanges.Should().BeTrue();
		await vm.SaveStepCommand.ExecuteAsync(null);
		vm.Status.Should().Be("« Hockey » ajoutée.");
		vm.IsEditing.Should().BeFalse("the form is ready for the next step");
		vm.HasUnsavedChanges.Should().BeFalse();
		vm.StepTitle = "Ranger le sac";
		vm.ChooseIconCommand.Execute(IconCatalog.All.First(i => i.Image == "homework.png"));
		vm.StepTitle.Should().Be("Ranger le sac", "a typed title is never replaced");
		await vm.SaveStepCommand.ExecuteAsync(null);
		vm.Steps.Select(s => s.Title).Should().Equal("S'habiller", "Prendre le petit déjeuner", "Se brosser les dents", "Hockey", "Ranger le sac");
		vm.SelectedStep = vm.Steps[3];
		vm.Category.Should().Be("Sports");
		vm.HasUnsavedChanges.Should().BeFalse();
		vm.StepTitle = "Hockey sur glace";
		vm.HasUnsavedChanges.Should().BeTrue();
		await vm.SaveStepCommand.ExecuteAsync(null);
		vm.Status.Should().Be("« Hockey sur glace » modifiée.");
		vm.SelectedStep = vm.Steps[3];
		await vm.DeleteStepCommand.ExecuteAsync(null);
		vm.Status.Should().Be("« Hockey sur glace » supprimée.");
	}

	[Fact]
	public async Task Moving_at_the_edges_is_safe_and_copying_an_empty_day_clears_only_the_target()
	{
		await using var scope = new TestDatabase();
		var vm = Create(scope);
		await vm.LoadAsync();
		vm.SelectedStep = vm.Steps[0];
		await vm.MoveUpCommand.ExecuteAsync(null);
		vm.Steps[0].Title.Should().Be("S'habiller");
		await vm.MoveDownCommand.ExecuteAsync(null);
		vm.Steps.Select(s => s.Title).Should().Equal("Prendre le petit déjeuner", "S'habiller", "Se brosser les dents");
		vm.SelectedStep = vm.Steps.Last();
		await vm.MoveDownCommand.ExecuteAsync(null);
		vm.Steps.Last().Title.Should().Be("Se brosser les dents");
		var db = await scope.Database.ConnectionAsync();
		await db.ExecuteAsync("DELETE FROM RoutineStep WHERE Day = ?", DayOfWeek.Monday);
		await vm.LoadAsync();
		vm.CopyTargets = [ParentDashboardViewModel.Days[1]];
		await vm.DuplicateDayCommand.ExecuteAsync(null);
		(await db.Table<RoutineStep>().Where(s => s.Day == DayOfWeek.Tuesday).CountAsync()).Should().Be(0);
		(await db.Table<RoutineStep>().Where(s => s.Day == DayOfWeek.Wednesday).CountAsync()).Should().Be(9);
	}
}
