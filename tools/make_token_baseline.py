"""Keep the source's formatting tokens without redistributing its English text."""

import argparse
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PATTERNS = (
    r"\{[^{}]+\}",
    r"<[^>]+>",
    r"\[c/[^:\]]+:",
    r"\[(?:i(?:/s[\d.]+)?|rg(?:/s[\d.]+)?|cbuff|cbuffplr|centeritem):[^\]]+\]",
)


def flatten(data, prefix=""):
    for key, value in data.items():
        name = f"{prefix}.{key}" if prefix else key
        if isinstance(value, dict):
            yield from flatten(value, name)
        elif isinstance(value, str):
            yield name
        else:
            raise ValueError(f"Translation {name} is not a string")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path, help="Flat JSON mapping localization keys to audited English text")
    args = parser.parse_args()
    source = json.loads(args.source.read_text(encoding="utf-8-sig"))
    if not isinstance(source, dict) or any(not isinstance(v, str) for v in source.values()):
        raise ValueError("Source must be a flat JSON object with string values")
    keys = set()
    for path in (ROOT / "ThaiOfflineAllMods/Translations").glob("*.json"):
        keys.update(flatten(json.loads(path.read_text(encoding="utf-8-sig"))))
    baseline = {key: [re.findall(pattern, source[key]) for pattern in PATTERNS]
                for key in sorted(keys) if key in source}
    if not baseline:
        raise ValueError("No translation keys matched the supplied source")
    output = ROOT / "tests/fixtures/source-tokens.json"
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(baseline, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Saved token baseline for {len(baseline)} keys to {output}")


if __name__ == "__main__":
    main()
