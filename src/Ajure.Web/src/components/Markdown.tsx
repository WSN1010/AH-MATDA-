import type { ReactNode } from 'react'

/**
 * 명세 문서에 필요한 Markdown 부분집합만 React 요소로 직접 렌더링한다.
 * HTML 문자열을 만들지 않으므로 주입 위험이 없고, 줄 번호와 Requirement ID를
 * 그대로 살릴 수 있어 Finding 강조와 미니맵에 그대로 쓴다.
 */

const RID = /^(?:FR|NFR|AC|TD|UX|J|GOAL|RISK|HG|DEC|P)-\d{2,3}$/
const INLINE = /(`[^`]+`)|(\*\*[^*]+\*\*)|(\[[^\]]+\]\([^)]+\))/g
const HEADING_TAGS = ['h1', 'h2', 'h3', 'h4', 'h5', 'h6'] as const
const URI_SCHEME = /^[a-z][a-z0-9+.-]*:/i

function safeHref(value: string): string {
  const href = value.trim()
  if (!URI_SCHEME.test(href) || /^(?:https?:|mailto:)/i.test(href)) return href
  return '#'
}

export interface Heading {
  id: string
  text: string
  level: number
}

export function extractHeadings(markdown: string): Heading[] {
  const headings: Heading[] = []
  let index = 0
  let inFence = false
  for (const line of markdown.split('\n')) {
    if (line.startsWith('```')) inFence = !inFence
    if (inFence) continue
    const match = /^(#{1,6})\s+(.*)$/.exec(line)
    if (!match) continue
    headings.push({
      id: `md-h-${index}`,
      text: (match[2] ?? '').replace(/`/g, ''),
      level: match[1]?.length ?? 1,
    })
    index += 1
  }
  return headings
}

function renderInline(text: string, keyPrefix: string): ReactNode[] {
  const nodes: ReactNode[] = []
  let cursor = 0
  let key = 0
  INLINE.lastIndex = 0
  let match = INLINE.exec(text)

  while (match) {
    if (match.index > cursor) nodes.push(text.slice(cursor, match.index))
    const token = match[0]
    if (token.startsWith('`')) {
      const value = token.slice(1, -1)
      nodes.push(
        RID.test(value) ? (
          <code key={`${keyPrefix}-${key}`} className="md-rid" data-rid={value}>
            {value}
          </code>
        ) : (
          <code key={`${keyPrefix}-${key}`} className="md-code">
            {value}
          </code>
        ),
      )
    } else if (token.startsWith('**')) {
      nodes.push(<strong key={`${keyPrefix}-${key}`}>{token.slice(2, -2)}</strong>)
    } else {
      const linkMatch = /^\[([^\]]+)\]\(([^)]+)\)$/.exec(token)
      const href = linkMatch?.[2] ?? '#'
      nodes.push(
        <a key={`${keyPrefix}-${key}`} href={safeHref(href)}>
          {linkMatch?.[1] ?? token}
        </a>,
      )
    }
    cursor = match.index + token.length
    key += 1
    match = INLINE.exec(text)
  }

  if (cursor < text.length) nodes.push(text.slice(cursor))
  return nodes
}

function tableRow(line: string): string[] {
  return line
    .replace(/^\|/, '')
    .replace(/\|$/, '')
    .split('|')
    .map((cell) => cell.trim())
}

interface BlockProps {
  line: number
  hit: boolean
  children: ReactNode
}

function wrap(element: ReactNode, { line, hit }: Omit<BlockProps, 'children'>): ReactNode {
  return (
    <div key={`b-${line}`} className={hit ? 'md-block md-block--hit' : 'md-block'} data-line={line + 1}>
      {element}
    </div>
  )
}

export function Markdown({ content, highlightLine }: { content: string; highlightLine?: number | null }) {
  const lines = content.split('\n')
  const blocks: ReactNode[] = []
  const hitLine = highlightLine ?? -1
  let index = 0
  let headingIndex = 0

  const isHit = (start: number, end: number) => hitLine >= start + 1 && hitLine <= end + 1

  while (index < lines.length) {
    const line = lines[index] ?? ''

    if (line.trim() === '') {
      index += 1
      continue
    }

    if (line.startsWith('```')) {
      const start = index
      index += 1
      const body: string[] = []
      while (index < lines.length && !(lines[index] ?? '').startsWith('```')) {
        body.push(lines[index] ?? '')
        index += 1
      }
      index += 1
      blocks.push(
        wrap(
          <pre className="md-pre">
            <code>{body.join('\n')}</code>
          </pre>,
          { line: start, hit: isHit(start, index - 1) },
        ),
      )
      continue
    }

    const heading = /^(#{1,6})\s+(.*)$/.exec(line)
    if (heading) {
      const level = Math.min(heading[1]?.length ?? 1, 6)
      const id = `md-h-${headingIndex}`
      headingIndex += 1
      const text = heading[2] ?? ''
      const Tag = HEADING_TAGS[level - 1] ?? 'h6'
      blocks.push(
        wrap(
          <Tag id={id} className={`md-h md-h${level}`}>
            {renderInline(text, id)}
          </Tag>,
          { line: index, hit: isHit(index, index) },
        ),
      )
      index += 1
      continue
    }

    if (/^-{3,}$/.test(line.trim())) {
      blocks.push(wrap(<hr className="md-hr" />, { line: index, hit: false }))
      index += 1
      continue
    }

    if (line.startsWith('|')) {
      const start = index
      const rows: string[][] = []
      while (index < lines.length && (lines[index] ?? '').startsWith('|')) {
        const current = lines[index] ?? ''
        if (!/^\|[\s:|-]+\|$/.test(current.trim())) rows.push(tableRow(current))
        index += 1
      }
      const [head, ...body] = rows
      blocks.push(
        wrap(
          <div className="md-tablewrap">
            <table className="md-table">
              {head && (
                <thead>
                  <tr>
                    {head.map((cell, cellIndex) => (
                      <th key={cellIndex} scope="col">
                        {renderInline(cell, `th-${start}-${cellIndex}`)}
                      </th>
                    ))}
                  </tr>
                </thead>
              )}
              <tbody>
                {body.map((row, rowIndex) => (
                  <tr key={rowIndex}>
                    {row.map((cell, cellIndex) => (
                      <td key={cellIndex}>{renderInline(cell, `td-${start}-${rowIndex}-${cellIndex}`)}</td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>,
          { line: start, hit: isHit(start, index - 1) },
        ),
      )
      continue
    }

    if (line.startsWith('> ')) {
      const start = index
      const body: string[] = []
      while (index < lines.length && (lines[index] ?? '').startsWith('> ')) {
        body.push((lines[index] ?? '').slice(2))
        index += 1
      }
      blocks.push(
        wrap(<blockquote className="md-quote">{renderInline(body.join(' '), `q-${start}`)}</blockquote>, {
          line: start,
          hit: isHit(start, index - 1),
        }),
      )
      continue
    }

    const bulleted = /^[-*]\s+/.test(line)
    const numbered = /^\d+\.\s+/.test(line)
    if (bulleted || numbered) {
      const start = index
      const items: string[] = []
      const matches = (value: string) => (bulleted ? /^[-*]\s+/.test(value) : /^\d+\.\s+/.test(value))
      while (index < lines.length && matches(lines[index] ?? '')) {
        items.push((lines[index] ?? '').replace(/^([-*]|\d+\.)\s+/, ''))
        index += 1
      }
      const children = items.map((item, itemIndex) => (
        <li key={itemIndex}>{renderInline(item, `li-${start}-${itemIndex}`)}</li>
      ))
      blocks.push(
        wrap(
          bulleted ? <ul className="md-list">{children}</ul> : <ol className="md-list">{children}</ol>,
          { line: start, hit: isHit(start, index - 1) },
        ),
      )
      continue
    }

    const start = index
    const paragraph: string[] = []
    while (index < lines.length) {
      const current = lines[index] ?? ''
      if (
        current.trim() === '' ||
        /^#{1,6}\s+/.test(current) ||
        current.startsWith('|') ||
        current.startsWith('```') ||
        current.startsWith('> ') ||
        /^[-*]\s+/.test(current) ||
        /^\d+\.\s+/.test(current)
      )
        break
      paragraph.push(current)
      index += 1
    }
    blocks.push(
      wrap(<p className="md-p">{renderInline(paragraph.join(' '), `p-${start}`)}</p>, {
        line: start,
        hit: isHit(start, index - 1),
      }),
    )
  }

  return <div className="md">{blocks}</div>
}
