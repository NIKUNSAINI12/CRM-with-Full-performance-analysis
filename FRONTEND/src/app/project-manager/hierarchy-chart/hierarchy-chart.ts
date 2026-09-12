import { Component, OnInit, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService, AuthUser } from '../../Core/services/auth';
import { TicketService } from '../../Core/services/ticket.service';
import { TICKET_SERVICE_TOKEN } from '../../Core/injection-tokens';

interface TreeNode {
  id: number;
  name: string;
  role: string;
  userNumber: string;
  email: string;
  mobileNo?: string;
  children: TreeNode[];
  isExpanded?: boolean;
  managerId?: number;
}

@Component({
  selector: 'app-hierarchy-chart',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './hierarchy-chart.html',
  styleUrls: ['./hierarchy-chart.scss']
})
export class HierarchyChartComponent implements OnInit {
  public currentUser: AuthUser | null = null;
  public isSuperUser = false;
  public isLoading = false;
  public errorMessage = '';
  
  public rootNodes: TreeNode[] = [];
  public filteredRootNodes: TreeNode[] = [];
  public searchTerm = '';

  // Stats
  public totalPMs = 0;
  public totalDevelopers = 0;

  constructor(
    private authService: AuthService,
    @Inject(TICKET_SERVICE_TOKEN) private ticketService: TicketService
  ) {}

  ngOnInit(): void {
    this.currentUser = this.authService.getCurrentUser();
    const role = this.currentUser?.role;
    this.isSuperUser = role === 'SuperManager' || role === 'Super Admin';
    this.loadHierarchy();
  }

  async loadHierarchy(): Promise<void> {
    this.isLoading = true;
    this.errorMessage = '';
    try {
      const allDevs = await this.ticketService.getAssignees();

      // 1. Map all developers into TreeNode structure
      const nodeMap = new Map<number, TreeNode>();
      allDevs.forEach(dev => {
        nodeMap.set(dev.id, {
          id: dev.id,
          name: dev.fullName || dev.assigneeNumber || 'User',
          role: dev['role'] || 'Assignee',
          userNumber: dev.assigneeNumber || `USR-${dev.id}`,
          email: dev.email || 'N/A',
          mobileNo: dev.mobileNo,
          children: [],
          isExpanded: true,
          managerId: dev.managerId ? Number(dev.managerId) : undefined
        });
      });

      // 2. Build the tree structure by pushing children to parents
      const childIds = new Set<number>();
      nodeMap.forEach(node => {
        if (node.managerId && nodeMap.has(node.managerId)) {
          const parentNode = nodeMap.get(node.managerId);
          parentNode!.children.push(node);
          childIds.add(node.id);
        }
      });

      // 3. Separate root nodes from unassigned/isolated developers
      const unassignedDevs: TreeNode[] = [];
      const mainRoots: TreeNode[] = [];

      nodeMap.forEach(node => {
        // If a node is not a child of anyone in the current list
        if (!childIds.has(node.id)) {
          // If it also has no children itself, it's a truly unassigned developer
          if (node.children.length === 0) {
            unassignedDevs.push(node);
          } else {
            // It has children, so it's a manager root
            mainRoots.push(node);
          }
        }
      });

      // Stats
      let managersCount = 0;
      let developersCount = 0;
      nodeMap.forEach(node => {
        if (node.children.length > 0) {
          managersCount++;
        } else {
          developersCount++;
        }
      });
      this.totalPMs = managersCount;
      this.totalDevelopers = developersCount;

      const treeRoots: TreeNode[] = [...mainRoots];

      if (unassignedDevs.length > 0) {
        treeRoots.push({
          id: -999,
          name: 'Unassigned Developers',
          role: 'System Group',
          userNumber: 'SYSTEM',
          email: 'N/A',
          children: unassignedDevs,
          isExpanded: true
        });
      }

      this.rootNodes = treeRoots;

      this.filterHierarchy();
    } catch (err: any) {
      console.error('Failed to load hierarchy data:', err);
      this.errorMessage = 'Failed to load organization hierarchy chart.';
    } finally {
      this.isLoading = false;
    }
  }

  public filterHierarchy(): void {
    if (!this.searchTerm) {
      this.filteredRootNodes = JSON.parse(JSON.stringify(this.rootNodes));
      return;
    }

    const term = this.searchTerm.toLowerCase().trim();
    
    // Deep clone helper to filter nodes matching search term
    const filterNode = (node: TreeNode): TreeNode | null => {
      const isMatch = node.name.toLowerCase().includes(term) || 
                      node.role.toLowerCase().includes(term) ||
                      node.userNumber.toLowerCase().includes(term);
      
      const filteredChildren = node.children
        .map(child => filterNode(child))
        .filter(child => child !== null) as TreeNode[];

      if (isMatch || filteredChildren.length > 0) {
        return {
          ...node,
          children: filteredChildren,
          isExpanded: true
        };
      }
      return null;
    };

    this.filteredRootNodes = this.rootNodes
      .map(root => filterNode(root))
      .filter(root => root !== null) as TreeNode[];
  }

  public toggleExpand(node: TreeNode, event: Event): void {
    event.stopPropagation();
    node.isExpanded = !node.isExpanded;
  }

  public getInitials(name: string): string {
    if (!name) return 'U';
    return name.split(' ')
      .map(part => part[0])
      .slice(0, 2)
      .join('')
      .toUpperCase();
  }
}
