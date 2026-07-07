using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace PropertiesSearch
{
    /// <summary>
    /// Wraps one selected object and exposes only the properties / events whose
    /// name, display name, or category contains the query. Everything else —
    /// attributes, converters, editors, the Site (which keeps the designer's
    /// IEventBindingService and thus the Events view working), and crucially
    /// GetPropertyOwner (so edits hit the real component) — forwards to the
    /// original object.
    /// </summary>
    internal sealed class FilteredComponent : ICustomTypeDescriptor, IComponent
    {
        private readonly object _original;
        private readonly string _query;

        internal FilteredComponent(object original, string query)
        {
            _original = original;
            _query    = query ?? string.Empty;
        }

        internal object Original => _original;

        private bool Matches(MemberDescriptor member)
        {
            if (_query.Length == 0) return true;
            return Contains(member.Name)
                || Contains(member.DisplayName)
                || Contains(member.Category);
        }

        private bool Contains(string text) =>
            text != null && text.IndexOf(_query, StringComparison.OrdinalIgnoreCase) >= 0;

        // A property row also stays visible when any property nested under it
        // (via its TypeConverter, the same mechanism the grid uses to expand
        // rows) matches the query, at any depth. Nesting is user-defined and
        // unbounded, hence the depth cap and reference cycle guard.
        private const int MaxSearchDepth = 16;

        private bool MatchesDeep(PropertyDescriptor prop, object owner, int depth, HashSet<object> visited)
        {
            if (Matches(prop)) return true;
            if (depth >= MaxSearchDepth) return false;

            object value;
            try { value = prop.GetValue(owner); }
            catch { return false; }
            if (value == null || value is string || value.GetType().IsPrimitive) return false;
            if (!value.GetType().IsValueType && !visited.Add(value)) return false;

            try
            {
                var converter = prop.Converter;
                if (converter == null || !converter.GetPropertiesSupported()) return false;
                var children = converter.GetProperties(null, value, null);
                if (children == null) return false;
                foreach (PropertyDescriptor child in children)
                    if (MatchesDeep(child, value, depth + 1, visited)) return true;
            }
            catch { /* a misbehaving converter must not break filtering */ }
            return false;
        }

        // ── Filtered members ───────────────────────────────────────────────

        public PropertyDescriptorCollection GetProperties(Attribute[] attributes) =>
            Filter(TypeDescriptor.GetProperties(_original, attributes));

        public PropertyDescriptorCollection GetProperties() =>
            Filter(TypeDescriptor.GetProperties(_original));

        private PropertyDescriptorCollection Filter(PropertyDescriptorCollection props)
        {
            var list = new List<PropertyDescriptor>(props.Count);
            foreach (PropertyDescriptor p in props)
            {
                var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
                if (_query.Length == 0 || MatchesDeep(p, _original, 0, visited)) list.Add(p);
            }
            return new PropertyDescriptorCollection(list.ToArray(), readOnly: true);
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
            bool IEqualityComparer<object>.Equals(object x, object y) => ReferenceEquals(x, y);
            int IEqualityComparer<object>.GetHashCode(object obj) =>
                System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        public EventDescriptorCollection GetEvents(Attribute[] attributes) =>
            Filter(TypeDescriptor.GetEvents(_original, attributes));

        public EventDescriptorCollection GetEvents() =>
            Filter(TypeDescriptor.GetEvents(_original));

        private EventDescriptorCollection Filter(EventDescriptorCollection events)
        {
            var list = new System.Collections.Generic.List<EventDescriptor>(events.Count);
            foreach (EventDescriptor e in events)
                if (Matches(e)) list.Add(e);
            return new EventDescriptorCollection(list.ToArray(), readOnly: true);
        }

        // ── Forwarded type description ─────────────────────────────────────

        public AttributeCollection GetAttributes() => TypeDescriptor.GetAttributes(_original);
        public string GetClassName()               => TypeDescriptor.GetClassName(_original);
        public string GetComponentName()           => TypeDescriptor.GetComponentName(_original);
        public TypeConverter GetConverter()        => TypeDescriptor.GetConverter(_original);
        public EventDescriptor GetDefaultEvent()   => TypeDescriptor.GetDefaultEvent(_original);
        public PropertyDescriptor GetDefaultProperty() => TypeDescriptor.GetDefaultProperty(_original);
        public object GetEditor(Type editorBaseType)   => TypeDescriptor.GetEditor(_original, editorBaseType);
        public object GetPropertyOwner(PropertyDescriptor pd) => _original;

        public override string ToString() => _original.ToString();

        // ── IComponent (Site keeps designer services reachable) ────────────

        public ISite Site
        {
            get => (_original as IComponent)?.Site;
            set { /* the proxy never owns the site */ }
        }

        // The proxy is transient; nothing to dispose and the original is never
        // disposed through it.
        public event EventHandler Disposed { add { } remove { } }
        public void Dispose() { }
    }
}
