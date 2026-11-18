using AwesomeAssertions;
using NextUpKids.Features.ParentAccess;
using NSubstitute;

namespace NextUpKids.Tests.Features.ParentAccess;

public class ParentGateViewModelTests
{
	[Fact]
	public void Correct_answer_unlocks_but_a_new_gate_is_locked()
	{
		var random = Substitute.For<Random>();
		random.Next(10, 50).Returns(17, 24, 13, 29);
		var vm = new ParentGateViewModel(random);
		vm.Challenge.Should().Be("17 + 24 = ?");
		vm.Answer = "41";
		vm.SubmitCommand.Execute(null);
		vm.IsUnlocked.Should().BeTrue();
		vm.Reset();
		vm.IsUnlocked.Should().BeFalse();
		vm.Answer.Should().BeEmpty();
		vm.Challenge.Should().Be("13 + 29 = ?");
	}

	[Theory]
	[InlineData("40")]
	[InlineData("")]
	[InlineData("not-a-number")]
	public void Incorrect_answer_stays_locked_and_replaces_the_challenge(string answer)
	{
		var random = Substitute.For<Random>();
		random.Next(10, 50).Returns(17, 24, 13, 29);
		var vm = new ParentGateViewModel(random) { Answer = answer };
		vm.SubmitCommand.Execute(null);
		vm.IsUnlocked.Should().BeFalse();
		vm.Challenge.Should().Be("13 + 29 = ?");
		vm.Answer.Should().BeEmpty();
		vm.Error.Should().Be("Réessaie avec ce nouveau calcul.");
	}
}
