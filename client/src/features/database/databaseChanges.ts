import { DEFAULT_TITLE, MAX_CONFIRM_TAB_NAMES } from '../../shared/constants'
import type { NoteTab } from '../../shared/types'

type DatabaseTabSnapshot = Pick<NoteTab, 'title' | 'content'>

export type DatabaseSnapshot = Map<string, DatabaseTabSnapshot>

type DatabaseChangeSummary = {
  snapshotKnown: boolean
  currentTabs: string[]
  createdTabs: string[]
  changedTabs: string[]
  deletedTabs: string[]
}

export function createDatabaseSnapshot(tabs: NoteTab[]): DatabaseSnapshot {
  const snapshot: DatabaseSnapshot = new Map()

  for (const tab of tabs) {
    snapshot.set(tab.id, {
      title: tab.title,
      content: tab.content,
    })
  }

  return snapshot
}

export function getDatabaseChangeSummary(
  currentTabs: NoteTab[],
  snapshot: DatabaseSnapshot | null,
): DatabaseChangeSummary {
  if (!snapshot) {
    return {
      snapshotKnown: false,
      currentTabs: currentTabs.map((tab) => formatTabName(tab.title)),
      createdTabs: [],
      changedTabs: [],
      deletedTabs: [],
    }
  }

  const currentIds = new Set<string>()
  const createdTabs: string[] = []
  const changedTabs: string[] = []

  for (const tab of currentTabs) {
    currentIds.add(tab.id)

    const savedTab = snapshot.get(tab.id)
    if (!savedTab) {
      createdTabs.push(formatTabName(tab.title))
      continue
    }

    if (savedTab.title !== tab.title || savedTab.content !== tab.content) {
      changedTabs.push(formatTabName(tab.title))
    }
  }

  const deletedTabs: string[] = []
  for (const [tabId, savedTab] of snapshot) {
    if (!currentIds.has(tabId)) {
      deletedTabs.push(formatTabName(savedTab.title))
    }
  }

  return {
    snapshotKnown: true,
    currentTabs: [],
    createdTabs,
    changedTabs,
    deletedTabs,
  }
}

export function hasDatabaseChanges(summary: DatabaseChangeSummary) {
  if (!summary.snapshotKnown) {
    return summary.currentTabs.length > 0
  }

  return (
    summary.createdTabs.length +
      summary.changedTabs.length +
      summary.deletedTabs.length >
    0
  )
}

export function formatDatabaseSaveConfirmation(summary: DatabaseChangeSummary) {
  if (!summary.snapshotKnown) {
    return [
      'No database copy has been loaded this session.',
      'Save current tabs to database?',
      '',
      'This will replace database notes with the current tab set.',
      '',
      ...formatTabGroup('Tabs to save', summary.currentTabs, '*'),
    ].join('\n')
  }

  return [
    'Save these database changes?',
    '',
    ...formatTabGroup('New tabs', summary.createdTabs, '+'),
    ...formatTabGroup('Changed tabs', summary.changedTabs, '~'),
    ...formatTabGroup('Deleted tabs', summary.deletedTabs, '-'),
  ].join('\n')
}

function formatTabGroup(label: string, tabNames: string[], prefix: string) {
  if (tabNames.length === 0) {
    return []
  }

  const visibleTabNames = tabNames.slice(0, MAX_CONFIRM_TAB_NAMES)
  const hiddenCount = tabNames.length - visibleTabNames.length
  const lines = [
    `${label} (${tabNames.length}):`,
    ...visibleTabNames.map((tabName) => `${prefix} ${tabName}`),
  ]

  if (hiddenCount > 0) {
    lines.push(`${prefix} ...and ${hiddenCount} more`)
  }

  lines.push('')
  return lines
}

function formatTabName(title: string) {
  const name = title.trim().replace(/\s+/g, ' ') || DEFAULT_TITLE

  if (name.length <= 80) {
    return name
  }

  return `${name.slice(0, 77)}...`
}
