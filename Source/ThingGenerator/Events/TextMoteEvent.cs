using UnityEngine;

namespace AAM.Events;

[CreateAssetMenu(fileName = "TextMote", menuName = "Events/TextMoteEvent")]
public class TextMoteEvent : EventBase
{
    public override string EventID => "TextMote";

    public string Text = "";
    public string PartName = "";
    public Vector3 Offset;
    public Color Color = Color.white;
    public float TimeBeforeFadeStart = -1;

    public override void Expose()
    {
        Look(ref Text);
        Look(ref PartName);
        Look(ref Offset);
        Look(ref Color);
        Look(ref TimeBeforeFadeStart);
    }
}