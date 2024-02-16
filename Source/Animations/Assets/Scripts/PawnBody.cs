using System;
using System.Linq;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(AnimatedPart))]
public class PawnBody : MonoBehaviour
{
    private const string EAST = "Naked_Female_east";
    private const string EAST_HEAD = "Female_Average_Normal_east";
    private const string NORTH = "Naked_Female_north";
    private const string NORTH_HEAD = "Female_Average_Normal_north";
    private const string SOUTH = "Naked_Female_south";
    private const string SOUTH_HEAD = "Female_Average_Normal_south";

    private static readonly Vector2 headOffsetHorizontal = new Vector2(0.1f, 0.34f);

    public AnimatedPart Body => _part == null ? (_part = GetComponent<AnimatedPart>()) : _part;
    public AnimatedPart Head => _head == null ? (_head = FindHead()) : _head;
    public BodyDirection Direction;

    [NonSerialized]
    private AnimatedPart _part, _head;

    private AnimatedPart FindHead()
    {
        string n = Body.CustomName.Replace("Body", "Head");
        return GetComponentInParent<Animator>().transform.GetComponentsInChildren<AnimatedPart>().First(p => p.CustomName == n);
    }

    private void LateUpdate()
    {
        switch (Direction)
        {
            case BodyDirection.East:
                Body.FlipX = false;
                Head.FlipX = false;
                Body.TexturePath = EAST;
                Head.TexturePath = EAST_HEAD;
                Head.IdleOffset = headOffsetHorizontal;
                break;

            case BodyDirection.North:
                Body.FlipX = false;
                Head.FlipX = false;
                Body.TexturePath = NORTH;
                Head.TexturePath = NORTH_HEAD;
                Head.IdleOffset = new Vector2(0f, headOffsetHorizontal.y);
                break;

            case BodyDirection.West:
                Body.FlipX = true;
                Head.FlipX = true;
                Body.TexturePath = EAST;
                Head.TexturePath = EAST_HEAD;
                Head.IdleOffset = headOffsetHorizontal;
                break;
            case BodyDirection.South:
                Body.FlipX = true;
                Head.FlipX = true;
                Body.TexturePath = SOUTH;
                Head.TexturePath = SOUTH_HEAD;
                Head.IdleOffset = new Vector2(0f, headOffsetHorizontal.y);
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}

public enum BodyDirection
{
    North = 0,
    East = 1,
    South = 2,
    West = 3
}
