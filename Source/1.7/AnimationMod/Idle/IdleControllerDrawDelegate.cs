namespace AM.Idle;

// Comp: The type of the component that is being drawn.
// shouldBeActive: A reference to a boolean that indicates whether the component (and therefore modded rendering) should be active.
// doDefaultDraw: A reference to a boolean that indicates whether the default vanilla weapon draw should be performed.
// Avoid setting both booleans to true at the same time, as this will cause the weapon to be drawn twice and a warning will be logged.
public delegate void IdleControllerDrawDelegate(IdleControllerComp comp, ref bool shouldBeActive, ref bool doDefaultDraw);
