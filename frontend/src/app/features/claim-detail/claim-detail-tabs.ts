export const DETAIL_TABS = { overview: 0, parties: 1, reserves: 2, documents: 3, audit: 4 } as const;

const RELATED_ENTITY_TABS: Record<string, number> = {
  ReserveHistory: DETAIL_TABS.reserves,
  ClaimReserveComponent: DETAIL_TABS.reserves,
  ClaimDocument: DETAIL_TABS.documents,
  ClaimParty: DETAIL_TABS.parties,
};

export function tabForRelatedEntity(relatedEntityType: string | null): number | null {
  return relatedEntityType ? (RELATED_ENTITY_TABS[relatedEntityType] ?? null) : null;
}
