using UnityEngine;

// Rebuilds every prefab and flow scene. The room / bathroom / lounge builders only write prefabs (no scene files).
public static class RebuildAll
{
    public static void Run()
    {
        DormRoomAssetBuilder.BuildAndCapture();   Debug.Log("RebuildAll: 1-person room done");
        AccessibleRoomBuilder.BuildAndCapture();  Debug.Log("RebuildAll: accessible room done");
        Room2AssetBuilder.BuildAndCapture();      Debug.Log("RebuildAll: 2-person room done");
        BathroomAssetBuilder.BuildAndCapture();   Debug.Log("RebuildAll: bathroom done");
        LoungeBuilder.BuildAndCapture();          Debug.Log("RebuildAll: lounge done");
        RoomBathTestBuilder.BuildAndCapture();    Debug.Log("RebuildAll: room+bath combos done");
        NavBuilder.BuildRoomViews();              Debug.Log("RebuildAll: room views done");
        CorridorBuilder.BuildAndCapture();        Debug.Log("RebuildAll: corridor done");
        FloorViewBuilder.BuildAndCapture();       Debug.Log("RebuildAll: floor view done");
        WomenCorridorBuilder.BuildAndCapture();   Debug.Log("RebuildAll: women corridor done");
        WomenFloorViewBuilder.BuildAndCapture();  Debug.Log("RebuildAll: women floor view done");
    }
}
