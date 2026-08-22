export interface DiffRow {
  type: 'same' | 'add' | 'del'
  text: string
  /** 원본 기준 줄 번호. 추가된 줄은 null */
  line: number | null
}

/** 줄 단위 LCS diff. 명세 문서 크기(수백 줄)에서 충분히 빠르다. */
export function diffLines(before: string, after: string): DiffRow[] {
  const a = before.split('\n')
  const b = after.split('\n')
  const width = b.length + 1
  const table = new Uint32Array((a.length + 1) * width)
  const at = (i: number, j: number) => table[i * width + j] ?? 0

  for (let i = a.length - 1; i >= 0; i--) {
    for (let j = b.length - 1; j >= 0; j--) {
      table[i * width + j] = a[i] === b[j] ? at(i + 1, j + 1) + 1 : Math.max(at(i + 1, j), at(i, j + 1))
    }
  }

  const rows: DiffRow[] = []
  let i = 0
  let j = 0
  while (i < a.length && j < b.length) {
    if (a[i] === b[j]) {
      rows.push({ type: 'same', text: a[i] ?? '', line: i + 1 })
      i += 1
      j += 1
    } else if (at(i + 1, j) >= at(i, j + 1)) {
      rows.push({ type: 'del', text: a[i] ?? '', line: i + 1 })
      i += 1
    } else {
      rows.push({ type: 'add', text: b[j] ?? '', line: null })
      j += 1
    }
  }
  while (i < a.length) {
    rows.push({ type: 'del', text: a[i] ?? '', line: i + 1 })
    i += 1
  }
  while (j < b.length) {
    rows.push({ type: 'add', text: b[j] ?? '', line: null })
    j += 1
  }
  return rows
}

/** 변경 지점만 남기고 앞뒤 문맥 몇 줄을 붙인다. */
export function collapseUnchanged(rows: DiffRow[], context = 2): (DiffRow | { type: 'gap'; count: number })[] {
  const keep = new Set<number>()
  rows.forEach((row, index) => {
    if (row.type === 'same') return
    for (let offset = -context; offset <= context; offset++) {
      const target = index + offset
      if (target >= 0 && target < rows.length) keep.add(target)
    }
  })

  const output: (DiffRow | { type: 'gap'; count: number })[] = []
  let skipped = 0
  rows.forEach((row, index) => {
    if (keep.has(index)) {
      if (skipped > 0) {
        output.push({ type: 'gap', count: skipped })
        skipped = 0
      }
      output.push(row)
    } else {
      skipped += 1
    }
  })
  if (skipped > 0) output.push({ type: 'gap', count: skipped })
  return output
}
