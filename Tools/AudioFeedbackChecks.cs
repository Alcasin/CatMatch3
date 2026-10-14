using System;
using System.Reflection;
using System.Runtime.Serialization;
using CatMatch3.Core;
using CatMatch3.Runtime;
using UnityEngine;

// Standalone managed checks, deliberately separate from the established 101 Core EditMode tests.
public static class AudioFeedbackChecks
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static T Field<T>(object instance, string name) { return (T)instance.GetType().GetField(name).GetValue(instance); }
    public static void Main()
    {
        var audioType = typeof(BoardAudio);
        var gateType = audioType.Assembly.GetType("CatMatch3.Runtime.AudioCueGate", true);
        var gate = Activator.CreateInstance(gateType, true);
        var reset = gateType.GetMethod("Reset");
        var enter = gateType.GetMethod("TryEnter");
        Func<AudioCue,float,bool> accept = (cue, time) => (bool)enter.Invoke(gate, new object[] {cue,time});
        reset.Invoke(gate,null);
        Require(accept(AudioCue.Select,0) && !accept(AudioCue.Select,0.01f) && accept(AudioCue.Select,0.07f), "Selection rate limit failed");
        Require(accept(AudioCue.Striped,1) && !accept(AudioCue.Striped,1.02f) && accept(AudioCue.Striped,1.22f), "Chain mixing limit failed");
        foreach (var terminal in new[] {AudioCue.Win,AudioCue.Fail})
        {
            for (int round=0;round<100;round++)
            {
                reset.Invoke(gate,null);
                Require(accept(terminal,0), "Result not rearmed after reset");
                Require(!accept(AudioCue.Win,10) && !accept(AudioCue.Fail,20) && !accept(AudioCue.Match,30), "Duplicate/stale sound after terminal");
                // Gate consumption precedes the mute branch in BoardAudio.Play.
                Require(!accept(terminal,100), "Muted terminal could replay on unmute");
            }
        }
        Require(BoardAudio.ResultCue(GameplayState.Win)==AudioCue.Win && BoardAudio.ResultCue(GameplayState.Fail)==AudioCue.Fail,
            "Wrong terminal mapping");
        Require(!BoardAudio.ResultCue(GameplayState.Active).HasValue && !BoardAudio.ResultCue(GameplayState.Error).HasValue,
            "Nonterminal/error incorrectly emits a result cue");
        string[] slots={"selectClip","invalidClip","matchClip","stripedClip","comboClip","winClip","failClip"};
        var component = FormatterServices.GetUninitializedObject(audioType);
        var getClip = audioType.GetMethod("GetClip",BindingFlags.NonPublic|BindingFlags.Instance);
        var gain = audioType.GetMethod("Gain",BindingFlags.NonPublic|BindingFlags.Static);
        for (int i=0;i<slots.Length;i++)
        {
            // References only: no native AudioClip construction, audio playback or Unity lifecycle.
            var clip=FormatterServices.GetUninitializedObject(typeof(AudioClip));
            audioType.GetField(slots[i],BindingFlags.NonPublic|BindingFlags.Instance).SetValue(component,clip);
            Require(ReferenceEquals(getClip.Invoke(component,new object[]{(AudioCue)i}),clip),"Wrong serialized clip mapping: "+slots[i]);
            float volume=(float)gain.Invoke(null,new object[]{(AudioCue)i});
            Require(volume>0 && volume<=0.5f,"Unsafe cue gain");
        }
        var view=typeof(BoardView);
        var layoutMethod=view.GetMethod("CalculateLayout",BindingFlags.NonPublic|BindingFlags.Static);
        var soundMethod=view.GetMethod("CalculateSoundBounds",BindingFlags.NonPublic|BindingFlags.Static);
        float[,] sizes={{900,1600},{900,2000},{840,1800},{720,1280},{360,640},{320,480},{620,1100}};
        for(int i=0;i<sizes.GetLength(0);i++)
        {
            var layout=layoutMethod.Invoke(null,new object[]{sizes[i,0],sizes[i,1],2});
            float scale=Field<float>(layout,"Scale"), restartY=Field<float>(layout,"RestartY");
            var sound=(Rect)soundMethod.Invoke(null,new object[]{scale,restartY});
            Require(sound.xMin>160f*scale && sound.xMax<Field<float>(layout,"Width")/2 && sound.yMin>=0,
                "Mute control overlaps Restart or exceeds safe footer");
            Require(Math.Abs(sound.center.y-restartY)<0.001f && sound.height>=40,"Mute target placement/height failed");
        }
        Console.WriteLine("PASS: 7 clip/gain mappings, terminal/error mappings, cooldowns, 200 reset/result duplicate checks and 7 safe footer layouts.");
    }
}
