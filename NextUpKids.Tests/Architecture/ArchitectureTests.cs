using ArchUnitNET.Domain.Dependencies;
using ArchUnitNET.Fluent.Conditions;
using ArchUnitNET.Fluent.Slices;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using CommunityToolkit.Mvvm.ComponentModel;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using ArchModel = ArchUnitNET.Domain.Architecture;

namespace NextUpKids.Tests.Architecture;

public class ArchitectureTests
{
	const string Root = "NextUpKids";
	const string Features = "NextUpKids.Features";
	const string Common = "NextUpKids.Common";

	static readonly ArchModel App = new ArchLoader()
		.LoadAssemblies(typeof(MauiProgram).Assembly, typeof(ObservableObject).Assembly)
		.Build();

	static readonly (string Type, string Getter)[] SystemClock =
	[
		("System.DateTime", "get_Now"),
		("System.DateTime", "get_UtcNow"),
		("System.DateTime", "get_Today"),
		("System.DateTimeOffset", "get_Now"),
		("System.DateTimeOffset", "get_UtcNow"),
	];

	[Fact]
	public void Slices_never_reference_each_other() =>
		SliceRuleDefinition.Slices()
			.Matching($"{Features}.(*)..")
			.Should().NotDependOnEachOther()
			.Check(App);

	[Fact]
	public void Common_never_depends_on_features() =>
		Types().That().ResideInNamespaceOrBelow(Common)
			.Should().NotDependOnAny(Types().That().ResideInNamespaceOrBelow(Features))
			.Check(App);

	[Fact]
	public void ViewModels_live_in_a_slice_and_inherit_ObservableObject() =>
		Classes().That().ResideInNamespaceOrBelow(Root).And().HaveNameEndingWith("ViewModel")
			.Should().ResideInNamespaceOrBelow(Features)
			.AndShould().BeAssignableTo(typeof(ObservableObject))
			.WithoutRequiringPositiveResults()
			.Check(App);

	[Fact]
	public void ObservableObjects_are_named_ViewModel() =>
		Classes().That().ResideInNamespaceOrBelow(Root).And().AreAssignableTo(typeof(ObservableObject))
			.Should().HaveNameEndingWith("ViewModel")
			.WithoutRequiringPositiveResults()
			.Check(App);

	[Fact]
	public void ViewModels_and_services_never_touch_MAUI_controls() =>
		Classes().That().ResideInNamespaceOrBelow(Root).And().HaveNameMatching("(ViewModel|Service)$")
			.Should().NotDependOnAny(Types(true).That().ResideInNamespaceOrBelow("Microsoft.Maui.Controls"))
			.WithoutRequiringPositiveResults()
			.Check(App);

	[Fact]
	public void Time_is_read_through_TimeProvider_only() =>
		Types().That().ResideInNamespaceOrBelow(Features).Or().ResideInNamespaceOrBelow(Common)
			.Should().FollowCustomCondition(
				type =>
				{
					var calls = type.Dependencies.OfType<MethodCallDependency>()
						.Select(call => call.TargetMember)
						.Where(member => SystemClock.Any(c => member.DeclaringType.FullName == c.Type && member.Name.StartsWith(c.Getter + "(")))
						.Select(member => member.FullName)
						.Distinct()
						.ToList();
					return new ConditionResult(type, calls.Count == 0, "calls " + string.Join(", ", calls));
				},
				"read the clock through TimeProvider")
			.Check(App);
}
