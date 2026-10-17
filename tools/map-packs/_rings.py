"""Assemble OSM boundary relations into GeoJSON multipolygons.

A relation's `outer`/`inner` members are ways that arrive split and in no
useful order — consecutive ways share an endpoint but either may be reversed.
Chaining them back into closed rings is the whole job; skipping it produces
open lines that no polygon-clipping tool will accept.
"""
import json, sys
from collections import defaultdict

def rings(ways):
    """Chain ways into closed rings. Returns (closed_rings, unclosed_count)."""
    # index endpoints -> way indices still unused
    remaining = {i: list(w) for i, w in enumerate(ways) if len(w) >= 2}
    out, unclosed = [], 0
    while remaining:
        i, ring = remaining.popitem()
        while ring[0] != ring[-1]:
            tail = ring[-1]
            nxt = None
            for j, w in remaining.items():
                if w[0] == tail:
                    nxt = (j, w); break
                if w[-1] == tail:
                    nxt = (j, w[::-1]); break
            if nxt is None:
                unclosed += 1
                break
            j, w = nxt
            del remaining[j]
            ring.extend(w[1:])
        if ring[0] == ring[-1] and len(ring) >= 4:
            out.append(ring)
    return out, unclosed

def area(ring):
    """Shoelace, for deciding which outer ring an inner belongs to."""
    s = 0.0
    for (x1, y1), (x2, y2) in zip(ring, ring[1:]):
        s += x1 * y2 - x2 * y1
    return abs(s) / 2

def point_in_ring(pt, ring):
    x, y = pt
    inside = False
    for (x1, y1), (x2, y2) in zip(ring, ring[1:]):
        if (y1 > y) != (y2 > y) and x < (x2 - x1) * (y - y1) / (y2 - y1) + x1:
            inside = not inside
    return inside

def to_multipolygon(rel):
    outers_w, inners_w = [], []
    for m in rel.get('members', []):
        if m.get('type') != 'way' or 'geometry' not in m:
            continue
        coords = [(p['lon'], p['lat']) for p in m['geometry']]
        (outers_w if m.get('role') != 'inner' else inners_w).append(coords)

    outers, o_bad = rings(outers_w)
    inners, i_bad = rings(inners_w)
    if not outers:
        return None, o_bad + i_bad

    # Each inner ring belongs to the smallest outer ring containing it.
    polys = [[o] for o in outers]
    for inner in inners:
        best, best_area = None, None
        for idx, o in enumerate(outers):
            if point_in_ring(inner[0], o):
                a = area(o)
                if best_area is None or a < best_area:
                    best, best_area = idx, a
        if best is not None:
            polys[best].append(inner)
    return polys, o_bad + i_bad

if __name__ == '__main__':
    data = json.load(open(sys.argv[1]))
    feats, problems = [], []
    for rel in data['elements']:
        t = rel.get('tags', {})
        polys, bad = to_multipolygon(rel)
        name_en = t.get('name:en') or t.get('int_name') or str(rel['id'])
        if polys is None:
            problems.append((name_en, 'no closed outer ring'))
            continue
        if bad:
            problems.append((name_en, f'{bad} unclosed ring(s) dropped'))
        feats.append({
            'type': 'Feature',
            'properties': {
                'osm_id': rel['id'],
                'name_en': name_en,
                'name_ar': t.get('name', ''),
            },
            'geometry': {'type': 'MultiPolygon', 'coordinates': polys},
        })
    json.dump({'type': 'FeatureCollection', 'features': feats},
              open(sys.argv[2], 'w'))
    print(f'features: {len(feats)}')
    for n, why in problems:
        print(f'  ! {n}: {why}')
