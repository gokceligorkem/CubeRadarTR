using CubeRadar.Core.Pets;

namespace CubeRadar.Tests;

public class PetGoalProgressionTests
{
    static PetSnapshot View(PetProgress progress)=>new(1,1,true,"",new Dictionary<int,PetProgress>{{1002,progress}},[],0);
    [Fact] public void Rank_two_automatically_advances_the_old_rank_two_goal_to_max_with_75_remaining()
    {
        var goals=new Dictionary<int,int>{{1002,2}};var view=View(new(2,0,true));
        Assert.True(PetGoalProgression.Advance(goals,view));Assert.Equal(3,goals[1002]);Assert.Equal(75,view.Progress[1002].Remaining(goals[1002]));
        Assert.False(PetGoalProgression.Advance(goals,view));
    }
    [Fact] public void Ready_souls_and_pending_upgrade_do_not_change_the_goal_before_the_rank_is_confirmed()
    {
        var goals=new Dictionary<int,int>{{1002,2}};
        Assert.False(PetGoalProgression.Advance(goals,View(new(1,25,true))));
        Assert.False(PetGoalProgression.Advance(goals,View(new(2,25,false))));Assert.Equal(2,goals[1002]);
        Assert.False(PetGoalProgression.IsMaxed(new(3,0,false)));Assert.True(PetGoalProgression.IsMaxed(new(3,0,true)));
    }
    [Fact] public void A_max_goal_is_retained_and_unknown_rank_does_not_change_a_saved_selection()
    {
        var goals=new Dictionary<int,int>{{1002,3}};
        Assert.False(PetGoalProgression.Advance(goals,View(new(1,13,true))));
        Assert.False(PetGoalProgression.Advance(goals,View(new(null,null,false))));
        Assert.False(PetGoalProgression.Advance(goals,View(new(3,0,true))));Assert.Equal(3,goals[1002]);
    }
    [Fact] public void Promoting_a_completed_goal_does_not_repeat_the_previous_completion_alert()
    {
        var goals=new Dictionary<int,int>{{1002,2}};var gate=new PetGoalGate();var initial=View(new(1,24,true));
        Assert.Empty(gate.Take(initial,goals));Assert.Equal([1002],gate.Take(View(new(1,25,true,GainSerial:1)),goals));
        var upgraded=View(new(2,0,true,GainSerial:1));PetGoalProgression.Advance(goals,upgraded);Assert.Empty(gate.Take(upgraded,goals));
        Assert.Equal(75,upgraded.Progress[1002].Remaining(goals[1002]));
    }
    [Theory][InlineData(1,1,2,25)][InlineData(2,1,3,75)]
    public void An_older_saved_goal_moves_to_the_current_next_rank(int rank,int oldGoal,int newGoal,int remaining)
    {
        var goals=new Dictionary<int,int>{{1002,oldGoal}};var view=View(new(rank,0,true));
        Assert.True(PetGoalProgression.Advance(goals,view));Assert.Equal(newGoal,goals[1002]);Assert.Equal(remaining,view.Progress[1002].Remaining(goals[1002]));
    }
}
