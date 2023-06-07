using UnityEngine;

/// <summary>
/// Custom attribute class used in the context of the previous script.
/// </summary>
public class I2PreviewAttribute : PropertyAttribute
{
	/// <summary>
	/// The line height for the localized string translation. A value of 0 indicates auto-sizing.
	/// </summary>
	public readonly uint LineHeight = 0;

	/// <summary>
	/// Initializes a new instance of the <see cref="I2PreviewAttribute"/> class with the default auto-sizing line height.
	/// </summary>
	public I2PreviewAttribute() { }

	/// <summary>
	/// Initializes a new instance of the <see cref="I2PreviewAttribute"/> class with a custom line height.
	/// </summary>
	/// <param name="lineHeight">The line height for the localized string translation. A value of 0 indicates auto-sizing.</param>
	public I2PreviewAttribute(uint lineHeight)
	{
		LineHeight = lineHeight;
	}
}
