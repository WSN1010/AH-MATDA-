/**
 * 의존성 없이 ZIP(STORED, 무압축) 하나를 만든다.
 * 로컬 목 모드에서만 쓴다. 백엔드가 있으면 POST /api/spec-versions/{versionId}/export가 ZIP을 준다.
 */

const CRC_TABLE = (() => {
  const table = new Uint32Array(256)
  for (let i = 0; i < 256; i++) {
    let c = i
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1
    table[i] = c >>> 0
  }
  return table
})()

function crc32(bytes: Uint8Array): number {
  let c = 0xffffffff
  for (let i = 0; i < bytes.length; i++) c = (CRC_TABLE[(c ^ (bytes[i] ?? 0)) & 0xff] ?? 0) ^ (c >>> 8)
  return (c ^ 0xffffffff) >>> 0
}

function dosDateTime(date: Date): { time: number; date: number } {
  return {
    time: (date.getHours() << 11) | (date.getMinutes() << 5) | (date.getSeconds() >> 1),
    date: ((date.getFullYear() - 1980) << 9) | ((date.getMonth() + 1) << 5) | date.getDate(),
  }
}

export interface ZipEntry {
  path: string
  content: string
}

const LOCAL_HEADER = 30
const CENTRAL_HEADER = 46
const END_RECORD = 22

export function createZip(entries: ZipEntry[], now = new Date()): Blob {
  const encoder = new TextEncoder()
  const { time, date } = dosDateTime(now)

  const prepared = entries.map((entry) => {
    const name = encoder.encode(entry.path)
    const data = encoder.encode(entry.content)
    return { name, data, crc: crc32(data) }
  })

  const localSize = prepared.reduce((n, e) => n + LOCAL_HEADER + e.name.length + e.data.length, 0)
  const centralSize = prepared.reduce((n, e) => n + CENTRAL_HEADER + e.name.length, 0)
  const buffer = new Uint8Array(localSize + centralSize + END_RECORD)
  const view = new DataView(buffer.buffer)

  let offset = 0
  const offsets: number[] = []

  for (const entry of prepared) {
    offsets.push(offset)
    view.setUint32(offset, 0x04034b50, true) // local file header signature
    view.setUint16(offset + 4, 20, true) // version needed
    view.setUint16(offset + 6, 0x0800, true) // UTF-8 파일명 플래그
    view.setUint16(offset + 8, 0, true) // method: stored
    view.setUint16(offset + 10, time, true)
    view.setUint16(offset + 12, date, true)
    view.setUint32(offset + 14, entry.crc, true)
    view.setUint32(offset + 18, entry.data.length, true) // compressed size
    view.setUint32(offset + 22, entry.data.length, true) // uncompressed size
    view.setUint16(offset + 26, entry.name.length, true)
    view.setUint16(offset + 28, 0, true) // extra length
    buffer.set(entry.name, offset + LOCAL_HEADER)
    buffer.set(entry.data, offset + LOCAL_HEADER + entry.name.length)
    offset += LOCAL_HEADER + entry.name.length + entry.data.length
  }

  const centralStart = offset
  prepared.forEach((entry, index) => {
    view.setUint32(offset, 0x02014b50, true) // central directory signature
    view.setUint16(offset + 4, 20, true) // version made by
    view.setUint16(offset + 6, 20, true) // version needed
    view.setUint16(offset + 8, 0x0800, true)
    view.setUint16(offset + 10, 0, true)
    view.setUint16(offset + 12, time, true)
    view.setUint16(offset + 14, date, true)
    view.setUint32(offset + 16, entry.crc, true)
    view.setUint32(offset + 20, entry.data.length, true)
    view.setUint32(offset + 24, entry.data.length, true)
    view.setUint16(offset + 28, entry.name.length, true)
    view.setUint16(offset + 30, 0, true) // extra
    view.setUint16(offset + 32, 0, true) // comment
    view.setUint16(offset + 34, 0, true) // disk number
    view.setUint16(offset + 36, 0, true) // internal attrs
    view.setUint32(offset + 38, 0, true) // external attrs
    view.setUint32(offset + 42, offsets[index] ?? 0, true)
    buffer.set(entry.name, offset + CENTRAL_HEADER)
    offset += CENTRAL_HEADER + entry.name.length
  })

  view.setUint32(offset, 0x06054b50, true) // end of central directory
  view.setUint16(offset + 4, 0, true)
  view.setUint16(offset + 6, 0, true)
  view.setUint16(offset + 8, prepared.length, true)
  view.setUint16(offset + 10, prepared.length, true)
  view.setUint32(offset + 12, offset - centralStart, true)
  view.setUint32(offset + 16, centralStart, true)
  view.setUint16(offset + 20, 0, true)

  return new Blob([buffer], { type: 'application/zip' })
}
