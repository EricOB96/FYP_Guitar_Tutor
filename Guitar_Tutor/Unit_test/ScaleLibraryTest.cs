using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using GuitarTutor.Data;

[TestSuite]
public class ScaleLibraryTest
{
    private ScaleLibrary _library;

    [Before]
    public void Setup()
    {
        _library = new ScaleLibrary();
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(_library);
        _library._Ready(); 
    }

    [After]
    public void TearDown()
    {
        _library?.QueueFree();
        _library = null;
    }

    [TestCase]
    public void TestGetScaleMajor()
    {
        var scale = _library.GetScale("major");
        AssertThat(scale).IsNotNull();
        AssertString(scale.DisplayName).IsEqual("Major");
        AssertArray(scale.Intervals).ContainsExactly(new int[] { 0, 2, 4, 5, 7, 9, 11 });
        AssertString(scale.Description).IsEqual("The standard major scale");
    }

    [TestCase]
    public void TestGetScaleMinor()
    {
        var scale = _library.GetScale("minor");
        AssertThat(scale).IsNotNull();
        AssertString(scale.DisplayName).IsEqual("Minor");
        AssertArray(scale.Intervals).ContainsExactly(new int[] { 0, 2, 3, 5, 7, 8, 10 });
        AssertString(scale.Description).IsEqual("The natural minor scale");
    }

    [TestCase]
    public void TestGetScaleKeys()
    {
        var keys = _library.GetScaleKeys();
        AssertArray(keys).ContainsExactly(new string[] { "major", "minor", "pentatonic_major", "pentatonic_minor" });
    }

    [TestCase]
    public void TestGetScaleDisplayNames()
    {
        var names = _library.GetScaleDisplayNames();
        AssertArray(names).ContainsExactly(new string[] { "Major", "Minor", "Major Pentatonic", "Minor Pentatonic" });
    }

    [TestCase]
    public void TestGetScaleKeyFromDisplayName()
    {
        AssertString(_library.GetScaleKeyFromDisplayName("Major")).IsEqual("major");
        AssertString(_library.GetScaleKeyFromDisplayName("Major Pentatonic")).IsEqual("pentatonic_major");
        AssertString(_library.GetScaleKeyFromDisplayName("Nonexistent Scale")).IsEqual("nonexistent_scale");
    }

    [TestCase]
    public void TestCurrentRootNoteProperty()
    {
        bool signalEmitted = false;
        _library.RootNoteChanged += (string newRoot) => { signalEmitted = true; AssertString(newRoot).IsEqual("C"); };
        _library.CurrentRootNote = "C";
        AssertString(_library.CurrentRootNote).IsEqual("C");
        AssertBool(signalEmitted).IsTrue();
    }

    [TestCase]
    public void TestCurrentScaleTypeProperty()
    {
        bool signalEmitted = false;
        _library.ScaleTypeChanged += (string newType) => { signalEmitted = true; AssertString(newType).IsEqual("minor"); };
        _library.CurrentScaleType = "minor";
        AssertString(_library.CurrentScaleType).IsEqual("minor");
        AssertBool(signalEmitted).IsTrue();
    }

    [TestCase]
    public void TestGetScaleNotFound()
    {
        var scale = _library.GetScale("not_a_scale");
        AssertThat(scale).IsNull();
    }
}
