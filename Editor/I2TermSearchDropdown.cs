using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

/// <summary>
/// Displays the currently registered I2 terms in Unity's searchable dropdown.
/// </summary>
internal sealed class I2TermSearchDropdown : AdvancedDropdown
{
	readonly bool m_IsAvailable;
	readonly Action<string> m_OnSelected;
	readonly List<string> m_Terms;

	internal I2TermSearchDropdown(
		AdvancedDropdownState state,
		IEnumerable<string> terms,
		bool isAvailable,
		Action<string> onSelected)
		: base(state)
	{
		m_Terms = terms == null ? new List<string>() : new List<string>(terms);
		m_IsAvailable = isAvailable;
		m_OnSelected = onSelected;
		minimumSize = new Vector2(320f, 280f);
	}

	protected override AdvancedDropdownItem BuildRoot()
	{
		AdvancedDropdownItem root = new AdvancedDropdownItem("I2 Terms");
		root.id = 0;
		if (m_Terms.Count == 0)
		{
			AdvancedDropdownItem emptyItem = new AdvancedDropdownItem(
				m_IsAvailable ? "No I2 terms found" : "I2 term search unavailable");
			emptyItem.id = 1;
			emptyItem.enabled = false;
			root.AddChild(emptyItem);
			return root;
		}

		for (int i = 0; i < m_Terms.Count; i++)
		{
			root.AddChild(new TermItem(m_Terms[i], i + 1));
		}

		return root;
	}

	protected override void ItemSelected(AdvancedDropdownItem item)
	{
		TermItem termItem = item as TermItem;
		if (termItem != null && m_OnSelected != null)
		{
			m_OnSelected(termItem.Term);
		}
	}

	sealed class TermItem : AdvancedDropdownItem
	{
		internal TermItem(string term, int itemId)
			: base(term)
		{
			id = itemId;
			Term = term;
		}

		internal string Term { get; private set; }
	}
}
