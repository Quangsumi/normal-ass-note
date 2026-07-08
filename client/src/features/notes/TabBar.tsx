import type { KeyboardEvent, Ref } from 'react'
import type { NoteTab } from '../../shared/types'

type TabBarProps = {
  activeId: string
  draftTitle: string
  editingId: string | null
  tabBarRef: Ref<HTMLElement>
  tabs: NoteTab[]
  onCloseTab: (tabId: string) => void
  onCreateTab: () => void
  onDraftTitleChange: (title: string) => void
  onFinishRename: (tabId: string) => void
  onRenameKeyDown: (event: KeyboardEvent<HTMLInputElement>, tab: NoteTab) => void
  onSelectTab: (tabId: string) => void
  onStartRename: (tab: NoteTab) => void
}

export function TabBar({
  activeId,
  draftTitle,
  editingId,
  tabBarRef,
  tabs,
  onCloseTab,
  onCreateTab,
  onDraftTitleChange,
  onFinishRename,
  onRenameKeyDown,
  onSelectTab,
  onStartRename,
}: TabBarProps) {
  return (
    <nav aria-label="note tabs" className="tab-bar" contentEditable={false} ref={tabBarRef}>
      {tabs.map((tab) =>
        editingId === tab.id ? (
          <input
            aria-label="rename tab"
            autoFocus
            className="tab-rename"
            key={tab.id}
            onBlur={() => onFinishRename(tab.id)}
            onChange={(event) => onDraftTitleChange(event.target.value)}
            onKeyDown={(event) => onRenameKeyDown(event, tab)}
            value={draftTitle}
          />
        ) : (
          <span className="tab-item" key={tab.id}>
            <button
              aria-current={tab.id === activeId ? 'page' : undefined}
              className="tab-title"
              onClick={() => onSelectTab(tab.id)}
              onDoubleClick={() => onStartRename(tab)}
              type="button"
            >
              {tab.title}
            </button>
            <button
              aria-label={`close ${tab.title}`}
              className="tab-close"
              onClick={() => onCloseTab(tab.id)}
              type="button"
            >
              X
            </button>
          </span>
        ),
      )}
      <button type="button" onClick={onCreateTab}>
        +
      </button>
    </nav>
  )
}
