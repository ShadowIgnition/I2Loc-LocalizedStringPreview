using System;

/// <summary>
/// Pure layout calculations used by the Unity drawer.
/// </summary>
internal static class I2PreviewLayout
{
	internal const uint AutoMinimumLineCount = 2;
	internal const uint AutoMaximumLineCount = 5;
	internal const uint MaximumFixedLineCount = 20;

	const float OverflowEpsilon = 0.5f;
	const float MinimumWidth = 1f;

	internal static float GetDesiredViewportHeight(
		Func<float, float> measureHeight,
		float availableWidth,
		float lineHeight,
		uint requestedLineCount)
	{
		lineHeight = NormalizeLineHeight(lineHeight);
		availableWidth = NormalizeWidth(availableWidth);

		if (requestedLineCount > 0)
		{
			uint clampedLineCount = Math.Min(requestedLineCount, MaximumFixedLineCount);
			return GetPaddedLineHeight(lineHeight, clampedLineCount);
		}

		float measuredHeight = Measure(measureHeight, availableWidth, lineHeight);
		float minimumHeight = GetPaddedLineHeight(lineHeight, AutoMinimumLineCount);
		float maximumHeight = GetPaddedLineHeight(lineHeight, AutoMaximumLineCount);
		return Clamp(measuredHeight, minimumHeight, maximumHeight);
	}

	internal static I2PreviewContentLayout GetContentLayout(
		Func<float, float> measureHeight,
		float availableWidth,
		float viewportHeight,
		float lineHeight,
		float verticalScrollbarWidth)
	{
		lineHeight = NormalizeLineHeight(lineHeight);
		availableWidth = NormalizeWidth(availableWidth);
		viewportHeight = Math.Max(0f, NormalizeFinite(viewportHeight, 0f));
		verticalScrollbarWidth = Math.Max(0f, NormalizeFinite(verticalScrollbarWidth, 0f));

		float fullWidthHeight = Measure(measureHeight, availableWidth, lineHeight);
		bool needsScrolling = fullWidthHeight > viewportHeight + OverflowEpsilon;

		if (!needsScrolling)
		{
			return new I2PreviewContentLayout(availableWidth, viewportHeight, false);
		}

		float contentWidth = NormalizeWidth(availableWidth - verticalScrollbarWidth);
		float contentHeight = Math.Max(
			viewportHeight,
			Measure(measureHeight, contentWidth, lineHeight));

		return new I2PreviewContentLayout(contentWidth, contentHeight, true);
	}

	internal static float GetPaddedLineHeight(float lineHeight, uint lineCount)
	{
		lineHeight = NormalizeLineHeight(lineHeight);
		return (lineHeight * lineCount) + (lineHeight / 2f);
	}

	static float Measure(Func<float, float> measureHeight, float width, float fallbackHeight)
	{
		if (measureHeight == null)
		{
			return fallbackHeight;
		}

		return Math.Max(
			fallbackHeight,
			NormalizeFinite(measureHeight(width), fallbackHeight));
	}

	static float NormalizeLineHeight(float lineHeight)
	{
		return Math.Max(1f, NormalizeFinite(lineHeight, 1f));
	}

	static float NormalizeWidth(float width)
	{
		return Math.Max(MinimumWidth, NormalizeFinite(width, MinimumWidth));
	}

	static float NormalizeFinite(float value, float fallback)
	{
		return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
	}

	static float Clamp(float value, float minimum, float maximum)
	{
		return Math.Max(minimum, Math.Min(maximum, value));
	}
}

internal readonly struct I2PreviewContentLayout
{
	internal I2PreviewContentLayout(float contentWidth, float contentHeight, bool needsScrolling)
	{
		ContentWidth = contentWidth;
		ContentHeight = contentHeight;
		NeedsScrolling = needsScrolling;
	}

	internal float ContentWidth { get; }
	internal float ContentHeight { get; }
	internal bool NeedsScrolling { get; }
}
