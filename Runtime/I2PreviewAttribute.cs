// https://github.com/ShadowIgnition/I2Loc-LocalizedStringPreview
using UnityEngine;

/// <summary>
/// Custom attribute class used for creating a preview of I2 Localization's LocalizedString Type
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
	public I2PreviewAttribute()
#if UNITY_6000_0_OR_NEWER
		: base(true)
#endif
	{ }

	/// <summary>
	/// Initializes a new instance of the <see cref="I2PreviewAttribute"/> class with a custom line height.
	/// </summary>
	/// <param name="lineHeight">The line height for the localized string translation. A value of 0 indicates auto-sizing.</param>
	public I2PreviewAttribute(uint lineHeight)
#if UNITY_6000_0_OR_NEWER
		: base(true)
#endif
	{
		LineHeight = lineHeight;
	}
}
